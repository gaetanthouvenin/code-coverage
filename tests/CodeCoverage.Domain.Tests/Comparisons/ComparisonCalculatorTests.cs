// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonCalculatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Comparisons;

public sealed class ComparisonCalculatorTests
{
  #region Fields

  private const string OneLineChanged = "@@ -1,3 +1,3 @@\n a\n-b\n+B\n c";

  private const string TwoLinesAdded = "@@ -1,2 +1,4 @@\n a\n+x\n+y\n b";

  private static readonly PathPatternSet AllPaths = new([]);

  #endregion

  #region Methods

  [Fact]
  public void Patch_ShouldCountOnlyCoverableAddedLines()
  {
    // Lines 2 and 3 are added; line 3 cannot be covered (a comment, a brace): the head report does not hold it.
    var head = View(File("src/a.cs", (1, 1), (2, 0), (4, 5)));

    var patch = ComparisonCalculator.Patch(head, Diff(Changed("src/a.cs", TwoLinesAdded)));

    patch.Totals.ShouldBe(new CoverageTotals(0, 1, 0, 0, 0));
    patch.Coverage.ShouldBe(0m);
    patch.Files.ShouldHaveSingleItem().ShouldBe(new PatchFileCoverage("src/a.cs", new CoverageTotals(0, 1, 0, 0, 0), false));
  }

  [Fact]
  public void Patch_NoCoverableLine_ShouldBeNull()
  {
    var head = View(File("src/a.cs", (1, 1), (4, 5)));

    var patch = ComparisonCalculator.Patch(head, Diff(Changed("src/a.cs", TwoLinesAdded)));

    patch.Totals.Lines.ShouldBe(0);
    patch.Coverage.ShouldBeNull();
    patch.Files.ShouldBeEmpty();
  }

  [Fact]
  public void Patch_EmptyDiff_ShouldBeNull()
  {
    var patch = ComparisonCalculator.Patch(View(File("src/a.cs", (1, 1))), Diff());

    patch.Coverage.ShouldBeNull();
    patch.HasUnavailableDiff.ShouldBeFalse();
  }

  // A partial line counts in the lines of the patch, never in its hits.
  [Fact]
  public void Patch_PartialLine_ShouldCountAsOneLineAndNoHit()
  {
    var head = View(FileCoverage.Create("src/a.cs", [new LineCoverage(2, 3, 1, 2, false, null), new LineCoverage(3, 3, 0, 0, false, null)]));

    var patch = ComparisonCalculator.Patch(head, Diff(Changed("src/a.cs", TwoLinesAdded)));

    patch.Totals.Hits.ShouldBe(1);
    patch.Totals.Partials.ShouldBe(1);
    patch.Totals.Lines.ShouldBe(2);
    patch.Coverage.ShouldBe(50m);
  }

  [Theory]
  [InlineData(FileDiffStatus.Added, true)]
  [InlineData(FileDiffStatus.Modified, true)]
  [InlineData(FileDiffStatus.Renamed, true)]
  [InlineData(FileDiffStatus.Copied, true)]
  [InlineData(FileDiffStatus.Removed, false)]
  [InlineData(FileDiffStatus.Unchanged, false)]
  public void Patch_ShouldOnlyReadTheFilesADiffCanAddLinesTo(FileDiffStatus status, bool counted)
  {
    var head = View(File("src/a.cs", (2, 1), (3, 1)));

    var patch = ComparisonCalculator.Patch(head, Diff(FileDiff.Create("src/a.cs", null, status, TwoLinesAdded)));

    patch.Totals.Lines.ShouldBe(counted ? 2 : 0);
  }

  // The added lines carry the numbers of the new file: a renamed file is read under its new path.
  [Fact]
  public void Patch_RenamedFile_ShouldBeReadUnderItsNewPath()
  {
    var head = View(File("src/new.cs", (2, 1), (3, 0)), File("src/old.cs", (2, 0), (3, 0)));

    var patch = ComparisonCalculator.Patch(head, Diff(FileDiff.Create("src/new.cs", "src/old.cs", FileDiffStatus.Renamed, TwoLinesAdded)));

    patch.Totals.ShouldBe(new CoverageTotals(1, 1, 0, 0, 0));
  }

  [Fact]
  public void Patch_FileWithoutPatch_ShouldBeMarkedUnavailableAndCountForNothing()
  {
    var head = View(File("src/a.cs", (2, 1)), File("src/big.cs", (1, 0), (2, 0)));

    var patch = ComparisonCalculator.Patch(head, Diff(Changed("src/a.cs", TwoLinesAdded), Changed("src/big.cs", null)));

    patch.Totals.ShouldBe(new CoverageTotals(1, 0, 0, 0, 0));
    patch.HasUnavailableDiff.ShouldBeTrue();
    patch.Files.ShouldBe([new PatchFileCoverage("src/a.cs", new CoverageTotals(1, 0, 0, 0, 0), false), new PatchFileCoverage("src/big.cs", default, true)]);
  }

  [Fact]
  public void Patch_FileAbsentFromTheReport_ShouldCountForNothing()
  {
    var patch = ComparisonCalculator.Patch(View(File("src/other.cs", (2, 1))), Diff(Changed("docs/readme.md", TwoLinesAdded)));

    patch.Coverage.ShouldBeNull();
    patch.Files.ShouldBeEmpty();
  }

  [Fact]
  public void Patch_SeveralFiles_ShouldSumAndListThemByPath()
  {
    var head = View(File("src/b.cs", (2, 1), (3, 1)), File("src/a.cs", (2, 0), (3, 1)));

    var patch = ComparisonCalculator.Patch(head, Diff(Changed("src/b.cs", TwoLinesAdded), Changed("src/a.cs", TwoLinesAdded)));

    patch.Totals.ShouldBe(new CoverageTotals(3, 1, 0, 0, 0));
    patch.Coverage.ShouldBe(75m);
    patch.Files.Select(static f => f.Path).ShouldBe(["src/a.cs", "src/b.cs"]);
  }

  // A real patch of GitHub (tests/fixtures/github/patches): its added lines are 16, 17, 47, 58, 59, 139 to 141,
  // 147 and 223 to 229. Codecov counts, among them, the lines the head report holds.
  [Fact]
  public void Patch_RealGitHubPatch_ShouldMatchTheLinesItAdds()
  {
    var text = System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "github", "patches", "multiple-hunks.diff")).ReplaceLineEndings("\n");
    var head = View(File(".editorconfig", (13, 9), (16, 1), (17, 0), (45, 0), (46, 0), (47, 2), (58, 0), (142, 1), (147, 0), (223, 1), (229, 4), (230, 0)));

    var patch = ComparisonCalculator.Patch(head, Diff(Changed(".editorconfig", text)));

    patch.Totals.ShouldBe(new CoverageTotals(4, 3, 0, 0, 0));
    patch.Coverage.ShouldBe(57.14286m);
  }

  [Fact]
  public void Delta_ShouldBeUnrounded()
  {
    // 2/3 and 1/3, each kept with five decimals as Codecov stores them: the difference is not rounded again.
    var delta = ComparisonCalculator.Delta(new CoverageTotals(2, 1, 0, 0, 0), new CoverageTotals(1, 2, 0, 0, 0));

    delta.ShouldBe(66.66667m - 33.33333m);
    delta.ShouldBe(33.33334m);
  }

  [Fact]
  public void Delta_Drop_ShouldBeNegative()
  {
    ComparisonCalculator.Delta(new CoverageTotals(1, 1, 0, 0, 0), new CoverageTotals(3, 1, 0, 0, 0)).ShouldBe(-25m);
  }

  [Fact]
  public void Delta_Equal_ShouldBeZero()
  {
    ComparisonCalculator.Delta(new CoverageTotals(1, 1, 0, 0, 0), new CoverageTotals(2, 2, 0, 0, 0)).ShouldBe(0m);
  }

  [Fact]
  public void Delta_WithoutASideOrWithoutLines_ShouldBeNull()
  {
    var some = new CoverageTotals(1, 1, 0, 0, 0);

    ComparisonCalculator.Delta(some, null).ShouldBeNull();
    ComparisonCalculator.Delta(null, some).ShouldBeNull();
    ComparisonCalculator.Delta(some, default(CoverageTotals)).ShouldBeNull();
    ComparisonCalculator.Delta(default(CoverageTotals), some).ShouldBeNull();
  }

  [Fact]
  public void Files_ShouldListTheFilesOfTheDiffThatAReportCovers()
  {
    var @base = View(File("src/a.cs", (1, 1), (2, 0)), File("src/removed.cs", (1, 1)), File("src/untouched.cs", (1, 1)));
    var head = View(File("src/a.cs", (1, 1), (2, 1), (3, 0)), File("src/added.cs", (2, 1), (3, 1)), File("src/untouched.cs", (1, 0)));

    var files = ComparisonCalculator.Files(
      @base,
      head,
      Diff(
        Changed("src/a.cs", TwoLinesAdded),
        FileDiff.Create("src/removed.cs", null, FileDiffStatus.Removed, "@@ -1 +0,0 @@\n-x"),
        FileDiff.Create("src/added.cs", null, FileDiffStatus.Added, TwoLinesAdded),
        Changed("docs/readme.md", OneLineChanged)
      )
    );

    files.Select(static f => f.Path).ShouldBe(["src/a.cs", "src/added.cs", "src/removed.cs"]);

    var modified = files[0];
    modified.Base.ShouldBe(new CoverageTotals(1, 1, 0, 0, 0));
    modified.Head.ShouldBe(new CoverageTotals(2, 1, 0, 0, 0));
    modified.Patch.ShouldBe(new CoverageTotals(1, 1, 0, 0, 0));
    modified.MissingPatchLines.ShouldBe(1);
    modified.Delta.ShouldBe(66.66667m - 50m);

    var added = files[1];
    added.Base.ShouldBeNull();
    added.Delta.ShouldBeNull();
    added.Patch.ShouldBe(new CoverageTotals(2, 0, 0, 0, 0));

    var removed = files[2];
    removed.Head.ShouldBeNull();
    removed.Base.ShouldBe(new CoverageTotals(1, 0, 0, 0, 0));
    removed.Patch.Lines.ShouldBe(0);
    removed.Status.ShouldBe(FileDiffStatus.Removed);
  }

  [Fact]
  public void Files_RenamedFile_ShouldReadTheBaseUnderItsPreviousPath()
  {
    var @base = View(File("src/old.cs", (1, 1), (2, 0)));
    var head = View(File("src/new.cs", (1, 1), (2, 1)));

    var file = ComparisonCalculator.Files(@base, head, Diff(FileDiff.Create("src/new.cs", "src/old.cs", FileDiffStatus.Renamed, OneLineChanged))).ShouldHaveSingleItem();

    file.PreviousPath.ShouldBe("src/old.cs");
    file.Base.ShouldBe(new CoverageTotals(1, 1, 0, 0, 0));
    file.Head.ShouldBe(new CoverageTotals(2, 0, 0, 0, 0));
    file.Delta.ShouldBe(50m);
  }

  [Fact]
  public void Files_WithoutBase_ShouldKeepTheFilesOfTheHead()
  {
    var file = ComparisonCalculator.Files(null, View(File("src/a.cs", (2, 1))), Diff(Changed("src/a.cs", TwoLinesAdded))).ShouldHaveSingleItem();

    file.Base.ShouldBeNull();
    file.Head.ShouldBe(new CoverageTotals(1, 0, 0, 0, 0));
  }

  [Fact]
  public void Flags_CarriedForward_ShouldBeMarked()
  {
    SessionReport[] head =
    [
      new(["unit"], Report(File("src/a.cs", (2, 1), (3, 0)))),
      new(["integration"], Report(File("src/a.cs", (2, 0), (3, 0), (4, 1)))),
    ];
    SessionReport[] @base = [new(["unit"], Report(File("src/a.cs", (1, 1), (4, 0)))), new(["integration"], Report(File("src/a.cs", (4, 1))))];

    var flags = ComparisonCalculator.Flags(@base, head, new HashSet<string> { "integration" }, Diff(Changed("src/a.cs", TwoLinesAdded)), static _ => AllPaths);

    flags.Select(static f => f.Flag).ShouldBe(["integration", "unit"]);
    flags[0].CarriedForward.ShouldBeTrue();
    flags[1].CarriedForward.ShouldBeFalse();
    // Each flag is its own view: the hits of another flag on the same line do not count.
    flags[1].Head.ShouldBe(new CoverageTotals(1, 1, 0, 0, 0));
    flags[1].Base.ShouldBe(new CoverageTotals(1, 1, 0, 0, 0));
    flags[1].Delta.ShouldBe(0m);
    flags[1].Patch.Totals.ShouldBe(new CoverageTotals(1, 1, 0, 0, 0));
    flags[0].Head.ShouldBe(new CoverageTotals(1, 2, 0, 0, 0));
    flags[0].Patch.Totals.ShouldBe(new CoverageTotals(0, 2, 0, 0, 0));
  }

  [Fact]
  public void Flags_ShouldBeCaseSensitive()
  {
    SessionReport[] head = [new(["Unit"], Report(File("src/a.cs", (1, 1)))), new(["unit"], Report(File("src/a.cs", (1, 0))))];

    var flags = ComparisonCalculator.Flags(null, head, new HashSet<string> { "unit" }, Diff(), static _ => AllPaths);

    flags.Select(static f => f.Flag).ShouldBe(["Unit", "unit"]);
    flags[0].CarriedForward.ShouldBeFalse();
    flags[0].Head!.Value.Hits.ShouldBe(1);
    flags[1].CarriedForward.ShouldBeTrue();
    flags[1].Head!.Value.Hits.ShouldBe(0);
  }

  [Fact]
  public void Flags_OnOneSideOnly_ShouldLeaveTheOtherEmpty()
  {
    SessionReport[] head = [new(["new"], Report(File("src/a.cs", (1, 1))))];
    SessionReport[] @base = [new(["old"], Report(File("src/a.cs", (1, 0))))];

    var flags = ComparisonCalculator.Flags(@base, head, new HashSet<string>(), Diff(), static _ => AllPaths);

    flags.Select(static f => (f.Flag, HasBase: f.Base is not null, HasHead: f.Head is not null)).ShouldBe([("new", false, true), ("old", true, false)]);
    flags[0].Delta.ShouldBeNull();
    flags[1].Patch.ShouldBe(PatchCoverage.Empty);
  }

  [Fact]
  public void Flags_SessionsWithoutFlag_ShouldGiveNoFlag()
  {
    SessionReport[] head = [new([], Report(File("src/a.cs", (1, 1))))];

    ComparisonCalculator.Flags(null, head, new HashSet<string>(), Diff(), static _ => AllPaths).ShouldBeEmpty();
  }

  [Fact]
  public void Flags_ShouldKeepOnlyTheGivenPaths()
  {
    SessionReport[] head = [new(["unit"], Report(File("src/a.cs", (1, 1)), File("tests/t.cs", (1, 0))))];

    var flag = ComparisonCalculator.Flags(null, head, new HashSet<string>(), Diff(), static _ => new PathPatternSet([PathPattern.Parse("src/")])).ShouldHaveSingleItem();

    flag.Head.ShouldBe(new CoverageTotals(1, 0, 0, 0, 0));
  }

  // Each flag keeps its own paths, as its totals do at the finalization of the commit.
  [Fact]
  public void Flags_ShouldMeasureEachFlagOnItsOwnPaths()
  {
    SessionReport[] head = [new(["front"], Report(File("web/a.ts", (1, 1)), File("api/b.cs", (1, 0)))), new(["back"], Report(File("web/a.ts", (1, 0)), File("api/b.cs", (1, 1))))];

    var flags = ComparisonCalculator.Flags(null, head, new HashSet<string>(), Diff(), static flag => new PathPatternSet([PathPattern.Parse(flag == "front" ? "web/" : "api/")]));

    flags.Select(static f => (f.Flag, f.Head)).ShouldBe([("back", new CoverageTotals(1, 0, 0, 0, 0)), ("front", new CoverageTotals(1, 0, 0, 0, 0))]);
  }

  [Fact]
  public void CarriedForwardFlags_ShouldKeepTheFlagsWithoutAnyUploadedSession()
  {
    Session[] sessions =
    [
      Uploaded(1, "unit"),
      Carried(2, "unit", "integration"),
      Carried(3, "e2e"),
      Uploaded(4),
    ];

    ComparisonCalculator.CarriedForwardFlags(sessions).ShouldBe(["integration", "e2e"], ignoreOrder: true);
  }

  [Fact]
  public void CarriedForwardFlags_NoSession_ShouldBeEmpty()
  {
    ComparisonCalculator.CarriedForwardFlags([]).ShouldBeEmpty();
  }

  [Fact]
  public void Comparison_Delta_ShouldCompareTheTwoTotals()
  {
    var comparison = new Comparison(new CoverageTotals(3, 1, 0, 0, 0), new CoverageTotals(1, 1, 0, 0, 0), PatchCoverage.Empty, [], [], [], false);

    comparison.Delta.ShouldBe(25m);
    (comparison with { Base = null }).Delta.ShouldBeNull();
  }

  [Fact]
  public void NullArguments_ShouldThrow()
  {
    var view = View(File("a", (1, 1)));

    Should.Throw<ArgumentNullException>(() => ComparisonCalculator.Patch(null!, Diff()));
    Should.Throw<ArgumentNullException>(() => ComparisonCalculator.Patch(view, null!));
    Should.Throw<ArgumentNullException>(() => ComparisonCalculator.Files(null, null!, Diff()));
    Should.Throw<ArgumentNullException>(() => ComparisonCalculator.Files(null, view, null!));
    Should.Throw<ArgumentNullException>(() => ComparisonCalculator.Flags(null, null!, [], Diff(), static _ => AllPaths));
    Should.Throw<ArgumentNullException>(() => ComparisonCalculator.Flags(null, [], null!, Diff(), static _ => AllPaths));
    Should.Throw<ArgumentNullException>(() => ComparisonCalculator.Flags(null, [], [], null!, static _ => AllPaths));
    Should.Throw<ArgumentNullException>(() => ComparisonCalculator.Flags(null, [], [], Diff(), null!));
    Should.Throw<ArgumentNullException>(() => ComparisonCalculator.CarriedForwardFlags(null!));
  }

  private static Session Uploaded(int number, params string[] flags) => Session.FromUpload(1, number, 10 + number, flags, null, null, null, default);

  private static Session Carried(int number, params string[] flags) =>
    Session.CarriedForward(1, number, GitSha.Parse(new string('a', GitSha.Length)), 100 + number, null, [1], flags, null, null, null, default);

  private static FileCoverage File(string path, params (int Line, long Hits)[] lines) =>
    FileCoverage.Create(path, lines.Select(static line => new LineCoverage(line.Line, line.Hits, 0, 0, false, null)));

  private static CoverageReport Report(params FileCoverage[] files) => CoverageReport.Create(files);

  private static ReportView View(params FileCoverage[] files) => ReportView.Build([new SessionReport([], Report(files))], [], AllPaths);

  private static FileDiff Changed(string path, string? patch) => FileDiff.Create(path, null, FileDiffStatus.Modified, patch);

  private static DiffSet Diff(params FileDiff[] files) => new(files, Truncated: false, DiffSource.Compare);

  #endregion
}