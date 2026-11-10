// ------------------------------------------------------------------------------------------------
// <copyright file="ChangeDetectorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Tests.Comparisons;

public sealed class ChangeDetectorTests
{
  #region Methods

  [Fact]
  public void Detect_FileInDiff_ShouldBeExcluded()
  {
    var @base = Report(File("src/a.cs", (1, 1), (2, 1)));
    var head = Report(File("src/a.cs", (1, 1), (2, 0)));

    ChangeDetector.Detect(@base, head, Diff(FileDiff.Create("src/a.cs", null, FileDiffStatus.Modified, "@@ -9 +9 @@\n-x\n+y"))).ShouldBeEmpty();
  }

  // Whatever the diff does to the file, and even without a patch: its lines may have moved.
  [Theory]
  [InlineData(FileDiffStatus.Added)]
  [InlineData(FileDiffStatus.Removed)]
  [InlineData(FileDiffStatus.Modified)]
  [InlineData(FileDiffStatus.Renamed)]
  [InlineData(FileDiffStatus.Copied)]
  [InlineData(FileDiffStatus.Unchanged)]
  public void Detect_FileInDiffWhateverItsStatus_ShouldBeExcluded(FileDiffStatus status)
  {
    var @base = Report(File("src/a.cs", (1, 1)));
    var head = Report(File("src/a.cs", (1, 0)));

    ChangeDetector.Detect(@base, head, Diff(FileDiff.Create("src/a.cs", null, status, null))).ShouldBeEmpty();
  }

  [Fact]
  public void Detect_RenamedFile_ShouldExcludeBothPaths()
  {
    var @base = Report(File("src/old.cs", (1, 1)));
    var head = Report(File("src/new.cs", (1, 0)));

    ChangeDetector.Detect(@base, head, Diff(FileDiff.Create("src/new.cs", "src/old.cs", FileDiffStatus.Renamed, null))).ShouldBeEmpty();
  }

  [Fact]
  public void Detect_StateChangeOutsideDiff_ShouldReport()
  {
    var @base = Report(File("src/a.cs", (1, 1), (2, 1), (3, 0), (4, 0)));
    var head = Report(File("src/a.cs", (1, 1), (2, 0), (3, 0), (4, 5)));

    var change = ChangeDetector.Detect(@base, head, Diff(FileDiff.Create("src/other.cs", null, FileDiffStatus.Modified, null))).ShouldHaveSingleItem();

    change.ShouldBe(new IndirectChange("src/a.cs", IndirectChangeKind.Changed, 0, 0, 0, 0m));
    change.IsFailing.ShouldBeFalse();
  }

  [Fact]
  public void Detect_MoreMissesOutsideDiff_ShouldFail()
  {
    var @base = Report(File("src/a.cs", (1, 1), (2, 1), (3, 1), (4, 0)));
    var head = Report(File("src/a.cs", (1, 1), (2, 0), (3, 0), (4, 0)));

    var change = ChangeDetector.Detect(@base, head, Diff()).ShouldHaveSingleItem();

    change.DeltaHits.ShouldBe(-2);
    change.DeltaMisses.ShouldBe(2);
    change.DeltaPartials.ShouldBe(0);
    change.DeltaCoverage.ShouldBe(-50m);
    change.IsFailing.ShouldBeTrue();
  }

  [Fact]
  public void Detect_HitToPartial_ShouldCountAPartialAndFail()
  {
    var @base = Report(FileCoverage.Create("src/a.cs", [new LineCoverage(1, 4, 2, 2, false, null)]));
    var head = Report(FileCoverage.Create("src/a.cs", [new LineCoverage(1, 4, 1, 2, false, null)]));

    var change = ChangeDetector.Detect(@base, head, Diff()).ShouldHaveSingleItem();

    (change.DeltaHits, change.DeltaMisses, change.DeltaPartials).ShouldBe((-1, 0, 1));
    change.IsFailing.ShouldBeTrue();
  }

  [Fact]
  public void Detect_MissToHit_ShouldReportWithoutFailing()
  {
    var @base = Report(File("src/a.cs", (1, 0), (2, 0)));
    var head = Report(File("src/a.cs", (1, 3), (2, 0)));

    var change = ChangeDetector.Detect(@base, head, Diff()).ShouldHaveSingleItem();

    (change.DeltaHits, change.DeltaMisses, change.DeltaPartials).ShouldBe((1, -1, 0));
    change.DeltaCoverage.ShouldBe(50m);
    change.IsFailing.ShouldBeFalse();
  }

  // A line hit 1 time or 50 times is in the same state: only a change of state counts.
  [Fact]
  public void Detect_OnlyTheHitCountsDiffer_ShouldReportNothing()
  {
    var @base = Report(File("src/a.cs", (1, 1), (2, 0)));
    var head = Report(File("src/a.cs", (1, 50), (2, 0)));

    ChangeDetector.Detect(@base, head, Diff()).ShouldBeEmpty();
  }

  [Fact]
  public void Detect_LineAppearingOrDisappearingInAFile_ShouldCount()
  {
    var @base = Report(File("src/a.cs", (1, 1), (2, 1)));
    var head = Report(File("src/a.cs", (1, 1), (3, 0)));

    var change = ChangeDetector.Detect(@base, head, Diff()).ShouldHaveSingleItem();

    (change.DeltaHits, change.DeltaMisses, change.DeltaPartials).ShouldBe((-1, 1, 0));
    change.IsFailing.ShouldBeTrue();
  }

  [Fact]
  public void Detect_FileAppearing_ShouldReport()
  {
    var @base = Report(File("src/a.cs", (1, 1)));
    var head = Report(File("src/a.cs", (1, 1)), File("src/new.cs", (1, 1), (2, 0)));

    var change = ChangeDetector.Detect(@base, head, Diff()).ShouldHaveSingleItem();

    change.ShouldBe(new IndirectChange("src/new.cs", IndirectChangeKind.Appeared, 1, 1, 0, null));
    // Listed, never failing: Codecov does not note a new file.
    change.IsFailing.ShouldBeFalse();
  }

  [Fact]
  public void Detect_FileDisappearing_ShouldReport()
  {
    var @base = Report(File("src/a.cs", (1, 1)), File("src/gone.cs", (1, 1), (2, 0)));
    var head = Report(File("src/a.cs", (1, 1)));

    var change = ChangeDetector.Detect(@base, head, Diff()).ShouldHaveSingleItem();

    change.ShouldBe(new IndirectChange("src/gone.cs", IndirectChangeKind.Disappeared, -1, -1, 0, null));
    change.IsFailing.ShouldBeFalse();
  }

  [Fact]
  public void Detect_IdenticalReports_ShouldReportNothing()
  {
    var report = Report(File("src/a.cs", (1, 1), (2, 0)), File("src/b.cs", (1, 0)));

    ChangeDetector.Detect(report, report, Diff()).ShouldBeEmpty();
  }

  [Fact]
  public void Detect_EmptyReports_ShouldReportNothing()
  {
    ChangeDetector.Detect(Report(), Report(), Diff()).ShouldBeEmpty();
  }

  [Fact]
  public void Detect_SeveralFiles_ShouldListThemByPath()
  {
    var @base = Report(File("b.cs", (1, 1)), File("gone.cs", (1, 1)), File("a.cs", (1, 1)));
    var head = Report(File("b.cs", (1, 0)), File("a.cs", (1, 0)), File("new.cs", (1, 1)));

    ChangeDetector.Detect(@base, head, Diff()).Select(static c => c.Path).ShouldBe(["a.cs", "b.cs", "gone.cs", "new.cs"]);
  }

  [Fact]
  public void Detect_NullArgument_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => ChangeDetector.Detect(null!, Report(), Diff()));
    Should.Throw<ArgumentNullException>(() => ChangeDetector.Detect(Report(), null!, Diff()));
    Should.Throw<ArgumentNullException>(() => ChangeDetector.Detect(Report(), Report(), null!));
  }

  private static FileCoverage File(string path, params (int Line, long Hits)[] lines) =>
    FileCoverage.Create(path, lines.Select(static line => new LineCoverage(line.Line, line.Hits, 0, 0, false, null)));

  private static CoverageReport Report(params FileCoverage[] files) => CoverageReport.Create(files);

  private static DiffSet Diff(params FileDiff[] files) => new(files, Truncated: false, DiffSource.Compare);

  #endregion
}