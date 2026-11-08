// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageTotalsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Tests.Coverage;

public sealed class CoverageTotalsTests
{
  #region Methods

  [Fact]
  public void Coverage_WithoutLines_ShouldBeNull()
  {
    default(CoverageTotals).Coverage.ShouldBeNull();
  }

  [Fact]
  public void Coverage_ShouldIgnoreBranchesAndMethods()
  {
    var totals = new CoverageTotals(hits: 1, misses: 2, partials: 0, branches: 50, methods: 7);

    totals.Lines.ShouldBe(3);
    totals.Coverage.ShouldBe(33.33333m);
  }

  [Fact]
  public void Ctor_ShouldKeepEachCounter()
  {
    var totals = new CoverageTotals(hits: 1, misses: 2, partials: 3, branches: 4, methods: 5);

    totals.Hits.ShouldBe(1);
    totals.Misses.ShouldBe(2);
    totals.Partials.ShouldBe(3);
    totals.Branches.ShouldBe(4);
    totals.Methods.ShouldBe(5);
    totals.Lines.ShouldBe(6);
  }

  [Theory]
  [InlineData(-1, 0, 0, 0, 0, "hits")]
  [InlineData(1, -1, 0, 0, 0, "misses")]
  [InlineData(1, 0, -1, 0, 0, "partials")]
  [InlineData(1, 0, 0, -1, 0, "branches")]
  [InlineData(1, 0, 0, 0, -1, "methods")]
  public void Ctor_WithNegativeCounter_ShouldThrowArgumentOutOfRange(
    int hits,
    int misses,
    int partials,
    int branches,
    int methods,
    string parameter)
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => new CoverageTotals(hits, misses, partials, branches, methods)
    ).ParamName.ShouldBe(parameter);
  }

  [Fact]
  public void Ctor_WhenLinesExceedAnInteger_ShouldThrowOverflow()
  {
    Should.Throw<OverflowException>(() => new CoverageTotals(int.MaxValue, 1, 0, 0, 0));
  }

  [Fact]
  public void ToString_ShouldPrintTheCoverage()
  {
    new CoverageTotals(1, 1, 0, 0, 0).ToString().ShouldContain("Coverage");
  }

  #endregion
}
