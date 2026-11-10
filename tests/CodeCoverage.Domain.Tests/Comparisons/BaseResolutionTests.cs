// ------------------------------------------------------------------------------------------------
// <copyright file="BaseResolutionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Tests.Comparisons;

public sealed class BaseResolutionTests
{
  #region Methods

  [Fact]
  public void NoDiff_ShouldBeEmptyAndComplete()
  {
    var diff = BaseResolution.NoDiff();

    diff.Files.ShouldBeEmpty();
    diff.Complete.ShouldBeTrue();
  }

  [Theory]
  [InlineData(DiffSource.Compare)]
  [InlineData(DiffSource.PullFiles)]
  public void UnavailableDiff_ShouldBeEmptyAndTruncated(DiffSource source)
  {
    var diff = BaseResolution.UnavailableDiff(source);

    diff.Files.ShouldBeEmpty();
    diff.Truncated.ShouldBeTrue();
    diff.Source.ShouldBe(source);
  }

  [Theory]
  [InlineData(0, true)]
  [InlineData(1, false)]
  [InlineData(null, false)]
  public void IsExactBase_ShouldHoldOnlyWithoutAnyHop(int? hops, bool exact)
  {
    new BaseResolution(null, null, null, BaseResolution.NoDiff(), BaseResolution.NoDiff(), null, hops).IsExactBase.ShouldBe(exact);
  }

  [Theory]
  [InlineData(false, false, false)]
  [InlineData(true, false, true)]
  [InlineData(false, true, true)]
  public void DiffIncomplete_ShouldFollowEitherDiff(bool patchTruncated, bool comparisonTruncated, bool incomplete)
  {
    var resolution = new BaseResolution(null, null, null, Diff(patchTruncated), Diff(comparisonTruncated), null, null);

    resolution.DiffIncomplete.ShouldBe(incomplete);
  }

  private static DiffSet Diff(bool truncated) => truncated ? BaseResolution.UnavailableDiff(DiffSource.Compare) : BaseResolution.NoDiff();

  #endregion
}