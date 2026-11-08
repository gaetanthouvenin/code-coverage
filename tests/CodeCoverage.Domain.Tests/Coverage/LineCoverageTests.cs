// ------------------------------------------------------------------------------------------------
// <copyright file="LineCoverageTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Tests.Coverage;

public sealed class LineCoverageTests
{
  #region Methods

  // An LCOV line with both FN and BRDA is of type "m" in Codecov: the method flag wins.
  [Theory]
  [InlineData(0, false, LineKind.Line)]
  [InlineData(0, true, LineKind.Method)]
  [InlineData(2, false, LineKind.Branch)]
  [InlineData(2, true, LineKind.Method)]
  public void Kind_ShouldFollowMethodFlagThenBranchData(int total, bool isMethod, LineKind expected)
  {
    new LineCoverage(1, 1, 0, total, isMethod, null).Kind.ShouldBe(expected);
  }

  [Theory]
  [InlineData(0, false)]
  [InlineData(1, true)]
  public void IsBranch_ShouldDependOnTotal(int total, bool expected)
  {
    new LineCoverage(1, 1, 0, total, false, null).IsBranch.ShouldBe(expected);
  }

  [Fact]
  public void Equals_SameMissingBranchesInAnotherArray_ShouldBeEqual()
  {
    var first = new LineCoverage(1, 1, 1, 2, false, ["a", "b"]);
    var second = new LineCoverage(1, 1, 1, 2, false, ["a", "b"]);

    first.ShouldBe(second);
    (first == second).ShouldBeTrue();
    first.GetHashCode().ShouldBe(second.GetHashCode());
  }

  [Theory]
  [InlineData("a", "c")]
  [InlineData("a", "a,b")]
  [InlineData("a,b", "b,a")]
  public void Equals_OtherMissingBranches_ShouldNotBeEqual(string first, string second)
  {
    var line = new LineCoverage(1, 1, 1, 2, false, first.Split(','));

    line.ShouldNotBe(line with { MissingBranches = second.Split(',') });
  }

  [Fact]
  public void Equals_NullAndEmptyMissingBranches_ShouldNotBeEqual()
  {
    var line = new LineCoverage(1, 1, 2, 2, false, null);

    line.ShouldNotBe(line with { MissingBranches = [] });
  }

  [Fact]
  public void Equals_AnotherField_ShouldNotBeEqual()
  {
    var line = new LineCoverage(1, 1, 1, 2, false, null);

    line.ShouldNotBe(line with { Hits = 2 });
    line.ShouldNotBe(line with { IsMethod = true });
  }

  #endregion
}
