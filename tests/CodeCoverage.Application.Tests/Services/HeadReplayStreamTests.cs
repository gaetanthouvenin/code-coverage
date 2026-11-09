// ------------------------------------------------------------------------------------------------
// <copyright file="HeadReplayStreamTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;

namespace CodeCoverage.Application.Tests.Services;

public sealed class HeadReplayStreamTests
{
  #region Methods

  [Fact]
  public async Task ReadAsync_ShouldGiveTheHeadThenTheRest()
  {
    await using var stream = new HeadReplayStream([1, 2, 9], 2, new MemoryStream([3, 4]));
    using var all = new MemoryStream();

    await stream.CopyToAsync(all, TestContext.Current.CancellationToken);

    all.ToArray().ShouldBe(new byte[] { 1, 2, 3, 4 });
  }

  [Fact]
  public void Read_SmallBuffer_ShouldGiveTheHeadPieceByPiece()
  {
    using var stream = new HeadReplayStream([1, 2, 3], 3, new MemoryStream([4]));
    var buffer = new byte[2];

    stream.Read(buffer, 0, 2).ShouldBe(2);
    stream.Read(buffer, 0, 2).ShouldBe(1);
    stream.Read(buffer, 0, 2).ShouldBe(1);
    stream.Read(buffer, 0, 2).ShouldBe(0);
  }

  #endregion
}
