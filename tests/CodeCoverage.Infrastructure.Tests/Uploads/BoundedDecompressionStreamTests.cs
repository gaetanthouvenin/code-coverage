// ------------------------------------------------------------------------------------------------
// <copyright file="BoundedDecompressionStreamTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;

using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Infrastructure.Uploads;

namespace CodeCoverage.Infrastructure.Tests.Uploads;

public sealed class BoundedDecompressionStreamTests
{
  #region Fields

  private const int BombBytes = 64 * 1024 * 1024;

  private const int Limit = 1024 * 1024;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Read_ZipBomb_ShouldThrowOverLimit()
  {
    var bomb = Gzip(new byte[BombBytes]);
    bomb.Length.ShouldBeLessThan(Limit);
    await using var stream = new BoundedDecompressionStream(new MemoryStream(bomb), Limit);

    await Should.ThrowAsync<UploadTooLargeException>(() => stream.CopyToAsync(Stream.Null, Ct));
  }

  [Fact]
  public void Read_ZipBombSynchronously_ShouldThrowOverLimit()
  {
    using var stream = new BoundedDecompressionStream(new MemoryStream(Gzip(new byte[BombBytes])), Limit);

    Should.Throw<UploadTooLargeException>(() => stream.CopyTo(Stream.Null));
  }

  [Fact]
  public async Task Read_ExactlyTheLimit_ShouldRestoreTheContent()
  {
    var content = new byte[Limit];
    content[^1] = 7;
    await using var stream = new BoundedDecompressionStream(new MemoryStream(Gzip(content)), Limit);
    using var restored = new MemoryStream();

    await stream.CopyToAsync(restored, Ct);

    restored.ToArray().ShouldBe(content);
  }

  [Fact]
  public async Task Dispose_ShouldLeaveTheSourceOpen()
  {
    var source = new MemoryStream(Gzip([1, 2, 3]));
    await using (var stream = new BoundedDecompressionStream(source, Limit))
    {
      await stream.CopyToAsync(Stream.Null, Ct);
    }

    source.CanRead.ShouldBeTrue();
  }

  private static byte[] Gzip(byte[] content)
  {
    using var output = new MemoryStream();

    using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
    {
      gzip.Write(content);
    }

    return output.ToArray();
  }

  #endregion
}
