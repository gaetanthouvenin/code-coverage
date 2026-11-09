// ------------------------------------------------------------------------------------------------
// <copyright file="DiffSetTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Tests.Diffs;

public sealed class DiffSetTests
{
  #region Methods

  [Theory]
  [InlineData(false, true)]
  [InlineData(true, false)]
  public void Complete_ShouldBeTheOppositeOfTruncated(bool truncated, bool complete)
  {
    new DiffSet([], truncated, DiffSource.Compare).Complete.ShouldBe(complete);
  }

  [Fact]
  public void Complete_FileWithoutPatch_ShouldStayComplete()
  {
    var set = new DiffSet([FileDiff.Create("a.cs", null, FileDiffStatus.Modified, null)], false, DiffSource.PullFiles);

    set.Complete.ShouldBeTrue();
    set.Source.ShouldBe(DiffSource.PullFiles);
  }

  #endregion
}
