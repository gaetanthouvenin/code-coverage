// ------------------------------------------------------------------------------------------------
// <copyright file="SqlRawUploadStoreTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;
using CodeCoverage.Infrastructure.Uploads;

namespace CodeCoverage.Infrastructure.Tests.Uploads;

// Every method against two organizations, on both engines: the rows of the other one are neither read nor written.
[Collection(ExclusiveDatabaseCollection.Name)]
public sealed class SqlRawUploadStoreTests(DatabaseFixture database)
{
  #region Fields

  private const long TwoHundredMegabytes = 200L * 1024 * 1024;

  private const long MemoryCeiling = 32L * 1024 * 1024;

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_200MbStream_ShouldNotMaterialize(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);

    // The first write compiles the queries of the store, which stay in memory for the rest of the process.
    await store.WriteAsync(Guid.NewGuid(), Part(UploadPartKind.Report, [1, 2, 3]), Ct);

    var baseline = GC.GetTotalMemory(forceFullCollection: true);
    var peak = 0L;
    var content = new GeneratedReadStream(
      TwoHundredMegabytes,
      TwoHundredMegabytes / 10,
      () => peak = Math.Max(peak, GC.GetTotalMemory(forceFullCollection: true) - baseline)
    );

    var stored = await store.WriteAsync(Guid.NewGuid(), new UploadPart(UploadPartKind.Report, "big.xml", content), Ct);

    stored.UncompressedSize.ShouldBe(TwoHundredMegabytes);
    peak.ShouldBeGreaterThan(0);
    peak.ShouldBeLessThan(MemoryCeiling);
    (await context.UploadRawChunks.AsNoTracking().CountAsync(c => c.RawFileId == stored.Id, Ct)).ShouldBeGreaterThan(190);
    context.ChangeTracker.Entries<UploadRawChunkRecord>().ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Read_ShouldRestoreBytes(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();
    var content = Random3Megabytes();

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);
    await store.WriteAsync(uploadId, Part(UploadPartKind.Report, content), Ct);

    var read = (await ReadAllAsync(store, uploadId)).ShouldHaveSingleItem();

    read.Kind.ShouldBe(UploadPartKind.Report);
    read.FileName.ShouldBe("report.xml");
    read.Content.ShouldBe(content);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Read_Synchronously_ShouldRestoreBytes(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();
    var content = Random3Megabytes();

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);
    await store.WriteAsync(uploadId, Part(UploadPartKind.Report, content), Ct);

    await foreach (var part in store.OpenReadAsync(uploadId, Ct))
    {
      using var restored = new MemoryStream();
      part.Content.CopyTo(restored);
      restored.ToArray().ShouldBe(content);
    }
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_ShouldReturnTheHashAndSizesAndCutChunks(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();
    var content = Random3Megabytes();

    await using var context = organizations.ContextOf(organizations.FirstId);
    var stored = await Store(context).WriteAsync(uploadId, Part(UploadPartKind.Report, content), Ct);

    stored.Sha256.ShouldBe(SHA256.HashData(content));
    stored.UncompressedSize.ShouldBe(content.Length);

    var file = await context.UploadRawFiles.AsNoTracking().SingleAsync(f => f.Id == stored.Id, Ct);
    var chunks = await context.UploadRawChunks.AsNoTracking().Where(c => c.RawFileId == stored.Id).OrderBy(static c => c.ChunkIndex).ToListAsync(Ct);

    file.UploadPublicId.ShouldBe(uploadId);
    file.Sha256.ShouldBe(stored.Sha256);
    file.CompressedSize.ShouldBe(stored.CompressedSize);
    file.UncompressedSize.ShouldBe(content.Length);
    file.ExpiresAt.ShouldBe(Now.AddDays(30));
    chunks.Select(static c => c.ChunkIndex).ShouldBe(Enumerable.Range(0, chunks.Count));
    chunks.ShouldAllBe(static c => c.Data.Length <= SqlRawUploadStore.ChunkSize);
    chunks.Sum(static c => c.Data.Length).ShouldBe((int)stored.CompressedSize);
    GzipDetector.IsGzip(chunks[0].Data).ShouldBeTrue();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_GzipPart_ShouldStoreItAsReceivedAndHashItsContent(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();
    var content = Random3Megabytes();
    var gzip = Gzip(content);

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);
    var stored = await store.WriteAsync(uploadId, Part(UploadPartKind.Report, gzip), Ct);

    stored.CompressedSize.ShouldBe(gzip.Length);
    stored.UncompressedSize.ShouldBe(content.Length);
    stored.Sha256.ShouldBe(SHA256.HashData(content));

    var chunks = await context.UploadRawChunks.AsNoTracking().Where(c => c.RawFileId == stored.Id).OrderBy(static c => c.ChunkIndex).ToListAsync(Ct);

    chunks.SelectMany(static c => c.Data).ToArray().ShouldBe(gzip);
    (await ReadAllAsync(store, uploadId)).ShouldHaveSingleItem().Content.ShouldBe(content);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_EmptyPart_ShouldRestoreNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);
    var stored = await store.WriteAsync(uploadId, Part(UploadPartKind.Report, []), Ct);

    stored.UncompressedSize.ShouldBe(0);
    (await ReadAllAsync(store, uploadId)).ShouldHaveSingleItem().Content.ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Read_SeveralParts_ShouldKeepTheirOrderAndKinds(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);
    await store.WriteAsync(uploadId, Part(UploadPartKind.Report, [1], "a.xml"), Ct);
    await store.WriteAsync(uploadId, Part(UploadPartKind.Network, [2], "network"), Ct);
    await store.WriteAsync(Guid.NewGuid(), Part(UploadPartKind.Report, [3], "other.xml"), Ct);

    var read = await ReadAllAsync(store, uploadId);

    read.Select(static r => r.FileName).ShouldBe(["a.xml", "network"]);
    read.Select(static r => r.Kind).ShouldBe([UploadPartKind.Report, UploadPartKind.Network]);
    read[1].Content.ShouldBe([(byte)2]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_OverTheLimitAcrossParts_ShouldThrowAndKeepNothingOfThePart(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();
    var part = new byte[800 * 1024];

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context, maxMegabytes: 1);
    await store.WriteAsync(uploadId, Part(UploadPartKind.Report, part, "first.xml"), Ct);

    await Should.ThrowAsync<UploadTooLargeException>(() => store.WriteAsync(uploadId, Part(UploadPartKind.Report, part, "second.xml"), Ct));

    (await ReadAllAsync(store, uploadId)).ShouldHaveSingleItem().FileName.ShouldBe("first.xml");
    (await context.UploadRawChunks.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
    context.ChangeTracker.Entries<UploadRawFileRecord>().Count().ShouldBe(1);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_ZipBomb_ShouldThrowAndKeepNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();
    var bomb = Gzip(new byte[64 * 1024 * 1024]);

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context, maxMegabytes: 1);

    await Should.ThrowAsync<UploadTooLargeException>(() => store.WriteAsync(uploadId, Part(UploadPartKind.Report, bomb), Ct));

    (await context.UploadRawFiles.AsNoTracking().CountAsync(Ct)).ShouldBe(0);
    (await context.UploadRawChunks.AsNoTracking().CountAsync(Ct)).ShouldBe(0);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_CorruptedGzip_ShouldThrowAndKeepNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);

    await Should.ThrowAsync<InvalidDataException>(
      () => store.WriteAsync(Guid.NewGuid(), Part(UploadPartKind.Report, [0x1F, 0x8B, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09]), Ct)
    );

    (await context.UploadRawFiles.AsNoTracking().CountAsync(Ct)).ShouldBe(0);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_NetworkAtItsLimit_ShouldBeAccepted(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using var context = organizations.ContextOf(organizations.FirstId);

    var stored = await Store(context).WriteAsync(Guid.NewGuid(), Part(UploadPartKind.Network, Lines(SqlRawUploadStore.MaxNetworkFiles)), Ct);

    stored.UncompressedSize.ShouldBePositive();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_NetworkOverItsLimit_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);

    await Should.ThrowAsync<UploadTooLargeException>(
      () => store.WriteAsync(Guid.NewGuid(), Part(UploadPartKind.Network, Lines(SqlRawUploadStore.MaxNetworkFiles + 1)), Ct)
    );
  }

  // A last line without a line feed still counts.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_NetworkOverItsLimitWithoutFinalLineFeed_ShouldThrow(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var content = Lines(SqlRawUploadStore.MaxNetworkFiles).Concat("a"u8.ToArray()).ToArray();

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);

    await Should.ThrowAsync<UploadTooLargeException>(() => store.WriteAsync(Guid.NewGuid(), Part(UploadPartKind.Network, content), Ct));
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Write_ReportWithManyLines_ShouldNotCountThem(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);

    await using var context = organizations.ContextOf(organizations.FirstId);

    await Store(context).WriteAsync(Guid.NewGuid(), Part(UploadPartKind.Report, Lines(SqlRawUploadStore.MaxNetworkFiles + 1)), Ct);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task OpenRead_OtherOrganization_ShouldFindNothing(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();

    await using (var first = organizations.ContextOf(organizations.FirstId))
    {
      await Store(first).WriteAsync(uploadId, Part(UploadPartKind.Report, [1, 2]), Ct);
    }

    await using var second = organizations.ContextOf(organizations.SecondId);

    (await ReadAllAsync(Store(second), uploadId)).ShouldBeEmpty();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Delete_OtherOrganization_ShouldLeaveThePartsIntact(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();

    await using (var first = organizations.ContextOf(organizations.FirstId))
    {
      await Store(first).WriteAsync(uploadId, Part(UploadPartKind.Report, [1, 2]), Ct);
    }

    await using (var second = organizations.ContextOf(organizations.SecondId))
    {
      await Store(second).DeleteAsync(uploadId, Ct);
    }

    await using var reader = organizations.ContextOf(organizations.FirstId);

    (await ReadAllAsync(Store(reader), uploadId)).ShouldHaveSingleItem().Content.ShouldBe([(byte)1, (byte)2]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Delete_ShouldRemoveEveryPartAndItsChunks(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();
    var kept = Guid.NewGuid();

    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);
    await store.WriteAsync(uploadId, Part(UploadPartKind.Report, Random3Megabytes()), Ct);
    await store.WriteAsync(uploadId, Part(UploadPartKind.Network, [1]), Ct);
    await store.WriteAsync(kept, Part(UploadPartKind.Report, [2]), Ct);

    await store.DeleteAsync(uploadId, Ct);

    (await ReadAllAsync(store, uploadId)).ShouldBeEmpty();
    (await ReadAllAsync(store, kept)).ShouldHaveSingleItem();
    (await context.UploadRawFiles.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
    (await context.UploadRawChunks.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
  }

  [Fact]
  public async Task Delete_UnknownUpload_ShouldDoNothing()
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, DatabaseProvider.PostgreSql, Ct);

    await using var context = organizations.ContextOf(organizations.FirstId);

    await Store(context).DeleteAsync(Guid.NewGuid(), Ct);
  }

  private static UploadPart Part(UploadPartKind kind, byte[] content, string fileName = "report.xml") => new(kind, fileName, new MemoryStream(content));

  private static byte[] Random3Megabytes()
  {
    var content = new byte[3 * 1024 * 1024];
    new Random(42).NextBytes(content);

    return content;
  }

  private static byte[] Lines(int count) => Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("a\n", count)));

  private static byte[] Gzip(byte[] content)
  {
    using var output = new MemoryStream();

    using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
    {
      gzip.Write(content);
    }

    return output.ToArray();
  }

  private static async Task<List<(UploadPartKind Kind, string FileName, byte[] Content)>> ReadAllAsync(IRawUploadStore store, Guid uploadId)
  {
    var parts = new List<(UploadPartKind, string, byte[])>();

    await foreach (var part in store.OpenReadAsync(uploadId, Ct))
    {
      await using var content = part.Content;
      using var restored = new MemoryStream();
      await content.CopyToAsync(restored, Ct);
      parts.Add((part.Kind, part.FileName, restored.ToArray()));
    }

    return parts;
  }

  private static SqlRawUploadStore Store(CodeCoverageDbContext context, int maxMegabytes = 2048, DateTimeOffset? now = null)
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(now ?? Now);

    var uploads = A.Fake<IOptionsMonitor<UploadOptions>>();
    A.CallTo(() => uploads.CurrentValue).Returns(new UploadOptions { MaxDecompressedMegabytes = maxMegabytes });

    var retention = A.Fake<IOptionsMonitor<RetentionOptions>>();
    A.CallTo(() => retention.CurrentValue).Returns(new RetentionOptions { RawDays = 30 });

    return new SqlRawUploadStore(context, clock, uploads, retention);
  }

  // What can still be parsed again: an upload with a report at least, within its retention.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListExpired_ShouldNameTheUploadsWithoutAKeptReport(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextOf(organizations.FirstId);
    var store = Store(context);
    var kept = Guid.NewGuid();
    var networkOnly = Guid.NewGuid();
    var neverStored = Guid.NewGuid();

    await store.WriteAsync(kept, Part(UploadPartKind.Network, Lines(2)), Ct);
    await store.WriteAsync(kept, Part(UploadPartKind.Report, [1, 2]), Ct);
    await store.WriteAsync(networkOnly, Part(UploadPartKind.Network, Lines(2)), Ct);

    (await store.ListExpiredAsync([kept, networkOnly, neverStored, kept], Ct)).ShouldBe([networkOnly, neverStored], ignoreOrder: true);
    (await store.ListExpiredAsync([kept], Ct)).ShouldBeEmpty();
    (await store.ListExpiredAsync([], Ct)).ShouldBeEmpty();
  }

  // The retention of 30 days: kept until the last instant before it ends, expired from that instant on,
  // even though the purge has not removed the rows yet.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListExpired_AtTheEndOfTheRetention_ShouldNameTheUpload(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    await using var context = organizations.ContextOf(organizations.FirstId);
    var uploadId = Guid.NewGuid();

    await Store(context).WriteAsync(uploadId, Part(UploadPartKind.Report, [1, 2]), Ct);

    (await Store(context, now: Now.AddDays(30).AddSeconds(-1)).ListExpiredAsync([uploadId], Ct)).ShouldBeEmpty();
    (await Store(context, now: Now.AddDays(30)).ListExpiredAsync([uploadId], Ct)).ShouldBe([uploadId]);
    (await Store(context, now: Now.AddDays(31)).ListExpiredAsync([uploadId], Ct)).ShouldBe([uploadId]);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ListExpired_OtherOrganization_ShouldSeeNoRawFile(DatabaseProvider provider)
  {
    var organizations = await TwoOrganizationDatabase.CreateAsync(database, provider, Ct);
    var uploadId = Guid.NewGuid();

    await using (var first = organizations.ContextOf(organizations.FirstId))
    {
      await Store(first).WriteAsync(uploadId, Part(UploadPartKind.Report, [1, 2]), Ct);
    }

    await using var second = organizations.ContextOf(organizations.SecondId);

    (await Store(second).ListExpiredAsync([uploadId], Ct)).ShouldBe([uploadId]);
  }

  #endregion
}
