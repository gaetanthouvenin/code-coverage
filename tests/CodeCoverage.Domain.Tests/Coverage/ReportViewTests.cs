// ------------------------------------------------------------------------------------------------
// <copyright file="ReportViewTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Coverage;

public sealed class ReportViewTests
{
  #region Fields

  private static readonly PathPatternSet AllPaths = new([]);

  #endregion

  #region Methods

  [Fact]
  public void Build_WithoutFlag_ShouldEqualTheMergedReport()
  {
    SessionReport[] sessions =
    [
      Session(["a"], Report("src/A.cs", (1, 1), (2, 0))),
      Session(["b"], Report("src/A.cs", (2, 3), (3, 0))),
      Session([], Report("src/B.cs", (1, 1))),
    ];

    var view = ReportView.Build(sessions, [], AllPaths);
    var merged = ReportMerger.Merge(sessions.Select(session => session.Report));

    view.Totals.ShouldBe(merged.Totals);
    view.Report.Files.Select(file => file.Path).ShouldBe(merged.Files.Select(file => file.Path));
    view.Report.Files[0].Lines.ShouldBe(merged.Files[0].Lines);
  }

  [Fact]
  public void Build_WithFlag_ShouldMergeOnlySessionsCarryingIt()
  {
    SessionReport[] sessions =
    [
      Session(["a", "shared"], Report("src/A.cs", (1, 1))),
      Session(["a"], Report("src/A.cs", (2, 1))),
      Session(["b"], Report("src/A.cs", (3, 1))),
      Session([], Report("src/A.cs", (4, 1))),
    ];

    var view = ReportView.Build(sessions, ["a"], AllPaths);

    view.Report.Files[0].Lines.Select(line => line.Line).ShouldBe([1, 2]);
  }

  [Fact]
  public void Build_WithSeveralFlags_ShouldTakeTheSessionsOfEach()
  {
    SessionReport[] sessions =
    [
      Session(["a"], Report("src/A.cs", (1, 1))),
      Session(["b"], Report("src/A.cs", (2, 1))),
      Session(["c"], Report("src/A.cs", (3, 1))),
    ];

    var view = ReportView.Build(sessions, ["a", "b"], AllPaths);

    view.Report.Files[0].Lines.Select(line => line.Line).ShouldBe([1, 2]);
  }

  [Fact]
  public void Build_WithFlag_ShouldCompareNamesExactly()
  {
    SessionReport[] sessions = [Session(["Backend"], Report("src/A.cs", (1, 1)))];

    ReportView.Build(sessions, ["backend"], AllPaths).Report.Files.ShouldBeEmpty();
  }

  [Fact]
  public void Build_WithFlagPaths_ShouldFilterFiles()
  {
    SessionReport[] sessions =
    [
      Session(["a"], Report("src/A.cs", (1, 1))),
      Session(["a"], Report("tests/T.cs", (1, 1))),
    ];

    var view = ReportView.Build(sessions, ["a"], Paths("src/"));

    view.Report.Files.Select(file => file.Path).ShouldBe(["src/A.cs"]);
    view.Totals.Lines.ShouldBe(1);
  }

  [Fact]
  public void Build_WithPathsOnly_ShouldKeepEverySessionAndFilterFiles()
  {
    SessionReport[] sessions =
    [
      Session(["a"], Report("src/A.cs", (1, 1))),
      Session(["b"], Report("lib/L.cs", (1, 1))),
    ];

    var view = ReportView.Build(sessions, [], Paths("lib/"));

    view.Report.Files.Select(file => file.Path).ShouldBe(["lib/L.cs"]);
  }

  [Fact]
  public void Build_JoinedFalse_ShouldExcludeFromProject()
  {
    SessionReport[] sessions =
    [
      Session(["a"], Report("src/A.cs", (1, 0))),
      Session(["solo"], Report("src/A.cs", (1, 5), (2, 1)), joined: false),
    ];

    var view = ReportView.Build(sessions, [], AllPaths);

    view.Totals.Hits.ShouldBe(0);
    view.Totals.Misses.ShouldBe(1);
    view.Report.Files[0].Lines.Select(line => line.Line).ShouldBe([1]);
  }

  [Fact]
  public void Build_JoinedFalse_ShouldStayInTheViewOfItsFlag()
  {
    SessionReport[] sessions = [Session(["solo"], Report("src/A.cs", (1, 5)), joined: false)];

    var view = ReportView.Build(sessions, ["solo"], AllPaths);

    view.Totals.Hits.ShouldBe(1);
  }

  [Fact]
  public void Build_ShuffledSessions_ShouldGiveTheSameView()
  {
    SessionReport[] sessions =
    [
      Session(["a"], Report("src/A.cs", (1, 1), (2, 0))),
      Session(["a"], Report("src/A.cs", (2, 3), (3, 0))),
      Session(["a"], Report("src/B.cs", (1, 0))),
    ];

    var forward = ReportView.Build(sessions, ["a"], AllPaths);
    var backward = ReportView.Build(sessions.Reverse(), ["a"], AllPaths);

    backward.Totals.ShouldBe(forward.Totals);
    backward.Report.Files[0].Lines.ShouldBe(forward.Report.Files[0].Lines);
  }

  [Fact]
  public void Build_NoSession_ShouldBeEmpty()
  {
    var view = ReportView.Build([], ["a"], AllPaths);

    view.Report.Files.ShouldBeEmpty();
    view.Totals.Coverage.ShouldBeNull();
  }

  [Fact]
  public void TotalsOf_ShouldCountTheLinesOfTheView()
  {
    SessionReport[] sessions = [Session(["a"], Report("src/A.cs", (1, 1), (2, 0), (3, 1)))];

    var totals = ReportView.Build(sessions, ["a"], AllPaths).TotalsOf("src/A.cs", [1, 2, 99]);

    totals.Hits.ShouldBe(1);
    totals.Misses.ShouldBe(1);
  }

  [Fact]
  public void TotalsOf_FileOutsideTheView_ShouldBeZero()
  {
    SessionReport[] sessions = [Session(["a"], Report("src/A.cs", (1, 1)))];

    var totals = ReportView.Build(sessions, ["a"], Paths("lib/")).TotalsOf("src/A.cs", [1]);

    totals.Lines.ShouldBe(0);
  }

  private static SessionReport Session(string[] flags, CoverageReport report, bool joined = true)
  {
    return new SessionReport(flags, report, joined);
  }

  private static CoverageReport Report(string path, params (int Line, long Hits)[] lines)
  {
    return CoverageReport.Create(
      [FileCoverage.Create(path, lines.Select(line => new LineCoverage(line.Line, line.Hits, 0, 0, false, null)))]);
  }

  private static PathPatternSet Paths(params string[] patterns)
  {
    return new PathPatternSet(patterns.Select(PathPattern.Parse));
  }

  #endregion
}
