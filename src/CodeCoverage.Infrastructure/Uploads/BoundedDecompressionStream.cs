// ------------------------------------------------------------------------------------------------
// <copyright file="BoundedDecompressionStream.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;

using CodeCoverage.Domain.Contracts.Uploads;

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>Decompresses gzip content, and stops as soon as it produces more than a limit.</summary>
/// <param name="compressed">The compressed content, left open.</param>
/// <param name="maxDecompressedBytes">The largest number of bytes it may produce.</param>
/// <remarks>The limit is checked on what comes out, so a small compressed content cannot expand without bound.</remarks>
internal sealed class BoundedDecompressionStream(Stream compressed, long maxDecompressedBytes) : ReadOnlyStream
{
  #region Fields

  /// <summary>The decompressor.</summary>
  private readonly GZipStream _decompressor = new(compressed, CompressionMode.Decompress, leaveOpen: true);

  /// <summary>The number of bytes produced so far.</summary>
  private long _produced;

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <exception cref="UploadTooLargeException">The content expands beyond the limit.</exception>
  public override int Read(byte[] buffer, int offset, int count) => Counted(_decompressor.Read(buffer, offset, count));

  /// <inheritdoc />
  /// <exception cref="UploadTooLargeException">The content expands beyond the limit.</exception>
  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
    Counted(await _decompressor.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));

  /// <inheritdoc />
  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      _decompressor.Dispose();
    }

    base.Dispose(disposing);
  }

  /// <summary>Counts what was produced and refuses what is over the limit.</summary>
  /// <param name="read">The number of bytes produced by the last read.</param>
  /// <returns><paramref name="read" />.</returns>
  /// <exception cref="UploadTooLargeException">The content expands beyond the limit.</exception>
  private int Counted(int read)
  {
    _produced += read;

    return _produced > maxDecompressedBytes
             ? throw new UploadTooLargeException($"The decompressed content exceeds the limit of {maxDecompressedBytes} bytes.")
             : read;
  }

  #endregion
}
