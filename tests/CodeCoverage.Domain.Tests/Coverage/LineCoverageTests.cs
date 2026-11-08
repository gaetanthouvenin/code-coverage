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

  #endregion
}
