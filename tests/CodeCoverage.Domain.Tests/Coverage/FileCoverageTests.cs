// ------------------------------------------------------------------------------------------------
// <copyright file="FileCoverageTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Tests.Coverage;

public sealed class FileCoverageTests
{
  #region Methods

  [Fact]
  public void Create_UnsortedLines_ShouldSortThem()
  {
    var file = FileCoverage.Create("a.cs", [Plain(3, 1), Plain(1, 0), Plain(2, 5)]);

    file.Lines.Select(line => line.Line).ShouldBe([1, 2, 3]);
  }

  [Fact]
  public void Create_DuplicateLine_ShouldMergeIt()
  {
    var file = FileCoverage.Create("a.cs", [Plain(1, 2), Plain(1, 7)]);

    file.Lines.ShouldHaveSingleItem().Hits.ShouldBe(7);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void Create_LineBelowOne_ShouldThrow(int line)
  {
    Should.Throw<ArgumentOutOfRangeException>(() => FileCoverage.Create("a.cs", [Plain(line, 1)]));
  }

  [Theory]
  [InlineData("")]
  [InlineData("  ")]
  public void Create_BlankPath_ShouldThrow(string path)
  {
    Should.Throw<ArgumentException>(() => FileCoverage.Create(path, []));
  }

  [Fact]
  public void Totals_ShouldCountStates()
  {
    var file = FileCoverage.Create(
      "a.cs",
      [Plain(1, 3), Plain(2, 0), Branch(3, 1, 2), Branch(4, 2, 2), Branch(5, 0, 2)]);

    file.Totals.Hits.ShouldBe(2);
    file.Totals.Misses.ShouldBe(2);
    file.Totals.Partials.ShouldBe(1);
    file.Totals.Lines.ShouldBe(5);
  }

  // Codecov counts lines by type (totals.py): a method line with branch data is only a method.
  [Fact]
  public void Totals_ShouldCountLinesByKind()
  {
    var file = FileCoverage.Create(
      "a.cs",
      [
        Plain(1, 1),
        Branch(2, 0, 2),
        Branch(3, 2, 2),
        new LineCoverage(4, 1, 0, 0, true, null),
        new LineCoverage(5, 1, 1, 2, true, null),
      ]);

    file.Totals.Branches.ShouldBe(2);
    file.Totals.Methods.ShouldBe(2);
  }

  private static LineCoverage Plain(int line, long hits)
  {
    return new LineCoverage(line, hits, 0, 0, false, null);
  }

  private static LineCoverage Branch(int line, int covered, int total)
  {
    return new LineCoverage(line, 0, covered, total, false, null);
  }

  #endregion
}
