// ------------------------------------------------------------------------------------------------
// <copyright file="SqlRawUploadStore.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>Stores the parts of the uploads in the database, as gzip cut into chunks of 1 MiB.</summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="clock">The clock that dates the expiry.</param>
/// <param name="uploadOptions">The limits of the uploads.</param>
/// <param name="retentionOptions">How long the parts are kept.</param>
/// <remarks>
///   <para>
///     Each chunk is saved through the context as soon as it is full, then forgotten by it: the writes stay under the
///     guards of the organization (ADR-013), and a part of any size holds one chunk in memory. There is no transaction
///     around a part, which can take minutes: what a failure leaves is removed at once, or by its expiry.
///   </para>
///   <para>
///     Gzip content is stored as it was received. It is still decompressed once, to bound it and to hash what it holds;
///     any other content is compressed on the way in.
///   </para>
/// </remarks>
internal sealed class SqlRawUploadStore(
  CodeCoverageDbContext context,
  IClock clock,
  IOptionsMonitor<UploadOptions> uploadOptions,
  IOptionsMonitor<RetentionOptions> retentionOptions) : IRawUploadStore
{
  #region Fields

  /// <summary>The size of a chunk.</summary>
  public const int ChunkSize = 1024 * 1024;

  /// <summary>The largest number of lines of a network part.</summary>
  public const int MaxNetworkFiles = 100_000;

  /// <summary>The size of the blocks read from a part.</summary>
  private const int BlockSize = 81_920;

  /// <summary>The number of bytes in a megabyte.</summary>
  private const long BytesPerMegabyte = 1024 * 1024;

  #endregion

  #region Implementation of IRawUploadStore

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="part" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException">The kind of <paramref name="part" /> is not a defined value.</exception>
  /// <exception cref="ArgumentException">The name of <paramref name="part" /> is too long.</exception>
  /// <exception cref="InvalidDataException">The gzip content of <paramref name="part" /> is corrupted; nothing of it is kept.</exception>
  public async Task<StoredRawFile> WriteAsync(Guid uploadId, UploadPart part, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(part);
    ArgumentOutOfRangeException.ThrowIfUndefined(part.Kind);

    ArgumentException.ThrowIf(
      part.FileName.Length > UploadPart.FileNameMaxLength,
      $"The name cannot be longer than {UploadPart.FileNameMaxLength} characters.",
      nameof(part));

    var used = await context.UploadRawFiles
                            .Where(f => f.UploadPublicId == uploadId)
                            .SumAsync(static f => (long?)f.UncompressedSize, cancellationToken)
                            .ConfigureAwait(false);
    var limit = Math.Max(0, (uploadOptions.CurrentValue.MaxDecompressedMegabytes * BytesPerMegabyte) - (used ?? 0));
    var file = new UploadRawFileRecord
    {
      UploadPublicId = uploadId,
      Kind = part.Kind,
      FileName = part.FileName,
      ExpiresAt = clock.UtcNow.AddDays(retentionOptions.CurrentValue.RawDays),
    };

    context.UploadRawFiles.Add(file);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    try
    {
      return await StoreContentAsync(file, part.Content, limit, cancellationToken).ConfigureAwait(false);
    }
    catch
    {
      await RemoveFailedFileAsync(file).ConfigureAwait(false);

      throw;
    }
  }

  /// <inheritdoc />
  public async IAsyncEnumerable<UploadPart> OpenReadAsync(Guid uploadId, [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var files = await context.UploadRawFiles
                             .AsNoTracking()
                             .Where(f => f.UploadPublicId == uploadId)
                             .OrderBy(static f => f.Id)
                             .Select(static f => new { f.Id, f.Kind, f.FileName })
                             .ToListAsync(cancellationToken)
                             .ConfigureAwait(false);

    foreach (var file in files)
    {
      yield return new UploadPart(file.Kind, file.FileName, OpenContent(file.Id));
    }
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<Guid>> ListExpiredAsync(IReadOnlyCollection<Guid> uploadIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(uploadIds);

    if (uploadIds.Count == 0)
    {
      return [];
    }

    Guid[] ids = [.. uploadIds.Distinct()];
    var now = clock.UtcNow;
    var kept = await context.UploadRawFiles
                            .AsNoTracking()
                            .Where(f => ids.Contains(f.UploadPublicId) && f.Kind == UploadPartKind.Report && f.ExpiresAt > now)
                            .Select(static f => f.UploadPublicId)
                            .Distinct()
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

    return [.. ids.Except(kept)];
  }

  /// <inheritdoc />
  public async Task DeleteAsync(Guid uploadId, CancellationToken cancellationToken)
  {
    var files = context.UploadRawFiles.Where(f => f.UploadPublicId == uploadId);

    await context.UploadRawChunks
                 .Where(c => files.Select(static f => f.Id).Contains(c.RawFileId))
                 .ExecuteDeleteAsync(cancellationToken)
                 .ConfigureAwait(false);

    await files.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion

  #region Methods

  /// <summary>Stores the content of a part and completes its row.</summary>
  /// <param name="file">The row of the part.</param>
  /// <param name="content">The content.</param>
  /// <param name="limit">How many decompressed bytes it may hold.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>What was stored.</returns>
  /// <exception cref="UploadTooLargeException">The content exceeds a limit.</exception>
  private async Task<StoredRawFile> StoreContentAsync(UploadRawFileRecord file, Stream content, long limit, CancellationToken cancellationToken)
  {
    var head = new byte[GzipDetector.HeaderLength];
    var headLength = await content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
    var isGzip = GzipDetector.IsGzip(head.AsSpan(0, headLength));
    var sink = new ChunkWriteStream((index, data, token) => SaveChunkAsync(file.Id, index, data, token), ChunkSize);
    var source = new ReplayReadStream(content, head.AsMemory(0, headLength), isGzip ? sink.WriteAsync : null);
    var compressor = isGzip ? null : new GZipStream(sink, CompressionLevel.Fastest, leaveOpen: true);

    try
    {
      using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
      var lines = new NetworkLineCounter(file.Kind, MaxNetworkFiles);
      var uncompressed = compressor is null
                           ? await ReadGzipAsync(source, hash, lines, limit, cancellationToken).ConfigureAwait(false)
                           : await CompressAsync(source, compressor, hash, lines, limit, cancellationToken).ConfigureAwait(false);

      lines.Complete();

      if (compressor is not null)
      {
        await compressor.DisposeAsync().ConfigureAwait(false);
      }

      await sink.CompleteAsync(cancellationToken).ConfigureAwait(false);

      file.Sha256 = hash.GetHashAndReset();
      file.CompressedSize = sink.TotalBytes;
      file.UncompressedSize = uncompressed;
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

      return new StoredRawFile(file.Id, file.Sha256, file.CompressedSize, file.UncompressedSize);
    }
    catch
    {
      sink.Discard();
      compressor?.Dispose();

      throw;
    }
  }

  /// <summary>Reads gzip content, whose bytes reach the chunks through <paramref name="source" />.</summary>
  /// <param name="source">The content.</param>
  /// <param name="hash">The hash of the decompressed content.</param>
  /// <param name="lines">The counter of a network part.</param>
  /// <param name="limit">How many decompressed bytes it may hold.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The number of decompressed bytes.</returns>
  /// <exception cref="UploadTooLargeException">The content exceeds a limit.</exception>
  private static async Task<long> ReadGzipAsync(Stream source, IncrementalHash hash, NetworkLineCounter lines, long limit, CancellationToken cancellationToken)
  {
    long total = 0;
    var buffer = new byte[BlockSize];

    using (var decompressor = new BoundedDecompressionStream(source, limit))
    {
      int read;

      while ((read = await decompressor.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
      {
        hash.AppendData(buffer, 0, read);
        lines.Add(buffer.AsSpan(0, read));
        total += read;
      }
    }

    // What follows the end of the gzip content, which the decompressor did not read, belongs to the part as well.
    while (await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) > 0)
    {
    }

    return total;
  }

  /// <summary>Compresses a content that is not gzip into the chunks.</summary>
  /// <param name="source">The content.</param>
  /// <param name="compressor">The compressor, which writes to the chunks.</param>
  /// <param name="hash">The hash of the content.</param>
  /// <param name="lines">The counter of a network part.</param>
  /// <param name="limit">How many bytes it may hold.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The number of bytes.</returns>
  /// <exception cref="UploadTooLargeException">The content exceeds a limit.</exception>
  private static async Task<long> CompressAsync(
    Stream source,
    GZipStream compressor,
    IncrementalHash hash,
    NetworkLineCounter lines,
    long limit,
    CancellationToken cancellationToken)
  {
    long total = 0;
    var buffer = new byte[BlockSize];
    int read;

    while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
    {
      total += read;

      if (total > limit)
      {
        throw new UploadTooLargeException($"The content exceeds the limit of {limit} bytes.");
      }

      hash.AppendData(buffer, 0, read);
      lines.Add(buffer.AsSpan(0, read));
      await compressor.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
    }

    return total;
  }

  /// <summary>Opens the decompressed content of a stored part.</summary>
  /// <param name="fileId">The identifier of the part.</param>
  /// <returns>The content, read chunk by chunk.</returns>
  private GZipStream OpenContent(long fileId) =>
    new(
      new ChunkReadStream(
        (index, token) => ChunkQuery(fileId, index).SingleOrDefaultAsync(token),
        index => ChunkQuery(fileId, index).SingleOrDefault()
      ),
      CompressionMode.Decompress
    );

  /// <summary>Builds the query of one chunk.</summary>
  /// <param name="fileId">The identifier of the part.</param>
  /// <param name="index">The index of the chunk.</param>
  /// <returns>The query of its bytes.</returns>
  private IQueryable<byte[]?> ChunkQuery(long fileId, int index) =>
    context.UploadRawChunks
           .AsNoTracking()
           .Where(c => c.RawFileId == fileId && c.ChunkIndex == index)
           .Select(static c => (byte[]?)c.Data);

  /// <summary>Saves a chunk and forgets it, so that the context does not accumulate the part.</summary>
  /// <param name="fileId">The identifier of the part.</param>
  /// <param name="index">The index of the chunk.</param>
  /// <param name="data">Its bytes.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when it is saved.</returns>
  private async Task SaveChunkAsync(long fileId, int index, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
  {
    var chunk = new UploadRawChunkRecord { RawFileId = fileId, ChunkIndex = index, Data = data.ToArray() };

    context.UploadRawChunks.Add(chunk);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    context.Entry(chunk).State = EntityState.Detached;
  }

  /// <summary>Removes what a failed write left behind.</summary>
  /// <param name="file">The row of the part.</param>
  /// <returns>A task that completes when it is removed, or could not be: its expiry then removes it.</returns>
  private async Task RemoveFailedFileAsync(UploadRawFileRecord file)
  {
    foreach (var pending in context.ChangeTracker.Entries<UploadRawChunkRecord>().ToList())
    {
      pending.State = EntityState.Detached;
    }

    context.Entry(file).State = EntityState.Detached;

    var fileId = file.Id;

    try
    {
      await context.UploadRawChunks.Where(c => c.RawFileId == fileId).ExecuteDeleteAsync(CancellationToken.None).ConfigureAwait(false);
      await context.UploadRawFiles.Where(f => f.Id == fileId).ExecuteDeleteAsync(CancellationToken.None).ConfigureAwait(false);
    }
    catch
    {
      // The failure that led here is the one to report, and the expiry removes these rows.
    }
  }

  #endregion
}
