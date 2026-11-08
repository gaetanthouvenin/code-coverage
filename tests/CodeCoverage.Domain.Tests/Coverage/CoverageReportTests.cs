// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageReportTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Tests.Coverage;

public sealed class CoverageReportTests
{
  #region Methods

  [Fact]
  public void Create_ShouldSortFilesByOrdinalPath()
  {
    var report = CoverageReport.Create([File("b.cs", 1), File("a.cs", 1), File("B.cs", 1)]);

    report.Files.Select(file => file.Path).ShouldBe(["B.cs", "a.cs", "b.cs"]);
  }

  [Fact]
  public void Create_SamePathTwice_ShouldMergeLines()
  {
    var report = CoverageReport.Create(
      [
        FileCoverage.Create("a.cs", [Plain(1, 0), Plain(2, 4)]),
        FileCoverage.Create("a.cs", [Plain(1, 3)]),
      ]);

    var file = report.Files.ShouldHaveSingleItem();
    file.Lines.Select(line => (line.Line, line.Hits)).ShouldBe([(1, 3L), (2, 4L)]);
  }

  [Fact]
  public void Create_PathsDifferingByCase_ShouldStaySeparate()
  {
    CoverageReport.Create([File("a.cs", 1), File("A.cs", 1)]).Files.Count.ShouldBe(2);
  }

  [Fact]
  public void Create_FileWithoutLines_ShouldDropIt()
  {
    var report = CoverageReport.Create([FileCoverage.Create("a.cs", []), File("b.cs", 1)]);

    report.Files.ShouldHaveSingleItem().Path.ShouldBe("b.cs");
  }

  [Fact]
  public void Create_NoFile_ShouldGiveNullCoverage()
  {
    var report = CoverageReport.Create([]);

    report.Files.ShouldBeEmpty();
    report.FileTotals.ShouldBeEmpty();
    report.Totals.Coverage.ShouldBeNull();
  }

  [Fact]
  public void FileTotals_ShouldFollowFiles()
  {
    var report = CoverageReport.Create([File("b.cs", 0), File("a.cs", 1)]);

    report.FileTotals.ShouldBe(
      [
        new FileTotals("a.cs", new CoverageTotals(1, 0, 0, 0, 0)),
        new FileTotals("b.cs", new CoverageTotals(0, 1, 0, 0, 0)),
      ]);
  }

  [Fact]
  public void Totals_ShouldSumEveryCounterOfTheFiles()
  {
    var report = CoverageReport.Create(
      [
        FileCoverage.Create(
          "a.cs",
          [Plain(1, 1), new LineCoverage(2, 0, 1, 2, false, null)]),
        FileCoverage.Create("b.cs", [new LineCoverage(1, 0, 0, 0, true, null)]),
      ]);

    report.Totals.ShouldBe(new CoverageTotals(1, 1, 1, 1, 1));
    report.Totals.Coverage.ShouldBe(33.33333m);
  }

  [Fact]
  public void TotalsOf_ShouldCountOnlyTheCoverableLinesAsked()
  {
    var report = CoverageReport.Create(
      [FileCoverage.Create("a.cs", [Plain(1, 1), Plain(2, 0), Plain(3, 1)])]);

    var totals = report.TotalsOf("a.cs", [1, 2, 2, 99]);

    totals.Hits.ShouldBe(1);
    totals.Misses.ShouldBe(1);
    totals.Lines.ShouldBe(2);
  }

  [Fact]
  public void TotalsOf_UnknownPath_ShouldBeZero()
  {
    var report = CoverageReport.Create([File("a.cs", 1)]);

    var totals = report.TotalsOf("other.cs", [1]);

    totals.Lines.ShouldBe(0);
    totals.Coverage.ShouldBeNull();
  }

  private static FileCoverage File(string path, long hits)
  {
    return FileCoverage.Create(path, [Plain(1, hits)]);
  }

  private static LineCoverage Plain(int line, long hits)
  {
    return new LineCoverage(line, hits, 0, 0, false, null);
  }

  #endregion
}
