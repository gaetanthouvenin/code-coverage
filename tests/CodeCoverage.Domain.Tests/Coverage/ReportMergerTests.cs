// ------------------------------------------------------------------------------------------------
// <copyright file="ReportMergerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Tests.Coverage;

public sealed class ReportMergerTests
{
  #region Fields

  private const int Seed = 20261108;
  private const int Permutations = 200;
  private const int Triples = 5000;

  private static readonly string[] BranchIds = ["a", "b", "c", "d"];
  private static readonly string[] Paths = ["src/A.cs", "src/B.cs", "src/C.cs"];
  private static readonly int[] BranchTotals = [2, 3, 4];
  private static readonly long[] PlainHits = [0, 0, 1, 7];

  #endregion

  #region Methods

  [Theory]
  [InlineData(1, 5, 5)]
  [InlineData(1, 0, 1)]
  [InlineData(0, 0, 0)]
  public void MergeLine_HitsWithHits_ShouldTakeMax(long first, long second, long expected)
  {
    var merged = MergeBothWays(Plain(first), Plain(second));

    merged.Hits.ShouldBe(expected);
    merged.IsBranch.ShouldBeFalse();
  }

  [Theory]
  [InlineData(0, 4)]
  [InlineData(1, 4)]
  public void MergeLine_HitsWithBranch_ShouldMakeFullBranch(int covered, int total)
  {
    var merged = MergeBothWays(Plain(1), Branch(covered, total));

    merged.Covered.ShouldBe(total);
    merged.Total.ShouldBe(total);
  }

  [Fact]
  public void MergeLine_ZeroHitsWithBranch_ShouldKeepBranch()
  {
    var merged = MergeBothWays(Plain(0), Branch(1, 4, "x", "y", "z"));

    merged.Covered.ShouldBe(1);
    merged.Total.ShouldBe(4);
    merged.MissingBranches.ShouldBe(["x", "y", "z"]);
  }

  [Fact]
  public void MergeLine_BranchesWithMissingIds_ShouldIntersect()
  {
    var merged = MergeBothWays(Branch(1, 4, "x", "y", "z"), Branch(2, 4, "y", "z"));

    merged.MissingBranches.ShouldBe(["y", "z"]);
    merged.Covered.ShouldBe(2);
    merged.Total.ShouldBe(4);
  }

  [Fact]
  public void MergeLine_BranchesWithDisjointMissingIds_ShouldBeFull()
  {
    var merged = MergeBothWays(Branch(1, 2, "x"), Branch(1, 2, "y"));

    merged.Covered.ShouldBe(2);
    merged.MissingBranches.ShouldBeEmpty();
    CoverageMath.Classify(merged).ShouldBe(LineState.Hit);
  }

  [Fact]
  public void MergeLine_MoreMissingIdsThanTotal_ShouldStopAtZero()
  {
    string[] missing = ["1:12", "1:3", "10:22", "10:12"];

    var merged = MergeBothWays(Branch(0, 3, missing), Branch(0, 3, missing));

    merged.Covered.ShouldBe(0);
    merged.Total.ShouldBe(3);
  }

  [Fact]
  public void MergeLine_MissingIdsWithHitOnTheOtherSide_ShouldBeFull()
  {
    var merged = MergeBothWays(Branch(0, 2, "x", "y"), Branch(2, 2));

    merged.Covered.ShouldBe(2);
    merged.MissingBranches.ShouldBeEmpty();
  }

  // §6.5 used to take the best ratio here, which would make it a Partial 1/3.
  [Fact]
  public void MergeLine_MissingIdsWithPartialOnTheOtherSide_ShouldKeepTheIds()
  {
    var merged = MergeBothWays(Branch(0, 3, "z", "x", "y"), Branch(1, 3));

    merged.Covered.ShouldBe(0);
    merged.MissingBranches.ShouldBe(["x", "y", "z"]);
    CoverageMath.Classify(merged).ShouldBe(LineState.Miss);
  }

  // The expected values of the merge_branch cases are those of Codecov's test_merge.py.
  [Theory]
  [InlineData(1, 2, 2, 2, 2, 2)]
  [InlineData(0, 2, 2, 2, 2, 2)]
  [InlineData(1, 2, 2, 3, 2, 3)]
  [InlineData(1, 2, 1, 6, 1, 6)]
  [InlineData(0, 2, 0, 2, 0, 2)]
  [InlineData(1, 2, 1, 2, 1, 2)]
  [InlineData(3, 4, 1, 2, 3, 4)]
  [InlineData(2, 3, 1, 5, 2, 5)]
  public void MergeLine_BranchesWithoutIds_ShouldTakeGreatestOfEach(
    int firstCovered,
    int firstTotal,
    int secondCovered,
    int secondTotal,
    int expectedCovered,
    int expectedTotal)
  {
    var merged = MergeBothWays(
      Branch(firstCovered, firstTotal),
      Branch(secondCovered, secondTotal));

    merged.Covered.ShouldBe(expectedCovered);
    merged.Total.ShouldBe(expectedTotal);
    merged.MissingBranches.ShouldBeNull();
  }

  // The named differences with Codecov, where its result depends on the order of the sessions.
  [Theory]
  [InlineData(1, 1, 2, 2, 2)]
  [InlineData(1, 1, 0, 4, 4)]
  public void MergeLine_FullBranchWithAnotherTotal_ShouldKeepLargestTotal(
    int firstCovered,
    int firstTotal,
    int secondCovered,
    int secondTotal,
    int expected)
  {
    var merged = MergeBothWays(
      Branch(firstCovered, firstTotal),
      Branch(secondCovered, secondTotal));

    merged.Covered.ShouldBe(expected);
    merged.Total.ShouldBe(expected);
  }

  [Fact]
  public void MergeLine_MissingIdsWithAnotherTotal_ShouldKeepLargestTotal()
  {
    var merged = MergeBothWays(Branch(1, 2, "x"), Branch(1, 4, "x", "y", "z"));

    merged.Covered.ShouldBe(3);
    merged.Total.ShouldBe(4);
    merged.MissingBranches.ShouldBe(["x"]);
  }

  [Fact]
  public void MergeLine_MethodWithBranch_ShouldStayMethod()
  {
    var merged = MergeBothWays(Plain(1) with { IsMethod = true }, Branch(1, 2));

    merged.Kind.ShouldBe(LineKind.Method);
  }

  [Fact]
  public void MergeLine_BranchWithHits_ShouldKeepTheGreatestHits()
  {
    MergeBothWays(Plain(9), Branch(1, 2) with { Hits = 3 }).Hits.ShouldBe(9);
  }

  [Fact]
  public void MergeLine_DifferentLines_ShouldThrow()
  {
    Should.Throw<ArgumentException>(
      () => ReportMerger.MergeLine(Plain(1), Plain(1) with { Line = 2 }));
  }

  [Fact]
  public void MergeLine_RandomLines_ShouldBeCommutativeAndAssociative()
  {
    var random = new Random(Seed);

    for (var i = 0; i < Triples; i++)
    {
      var a = RandomLine(random, 1);
      var b = RandomLine(random, 1);
      var c = RandomLine(random, 1);

      var ab = ReportMerger.MergeLine(a, b);

      ab.ShouldBe(ReportMerger.MergeLine(b, a));
      ReportMerger.MergeLine(ab, c)
        .ShouldBe(ReportMerger.MergeLine(a, ReportMerger.MergeLine(b, c)));
    }
  }

  [Fact]
  public void Merge_ShuffledSessions_ShouldBeDeterministic()
  {
    var random = new Random(Seed);
    var sessions = Enumerable.Range(0, 8).Select(_ => RandomReport(random)).ToArray();
    var expected = ReportMerger.Merge(sessions);

    expected.Totals.Hits.ShouldBeGreaterThan(0);
    expected.Totals.Misses.ShouldBeGreaterThan(0);
    expected.Totals.Partials.ShouldBeGreaterThan(0);

    for (var i = 0; i < Permutations; i++)
    {
      random.Shuffle(sessions);

      var merged = ReportMerger.Merge(sessions);

      Flatten(merged).ShouldBe(Flatten(expected));
      merged.FileTotals.ShouldBe(expected.FileTotals);
      merged.Totals.ShouldBe(expected.Totals);
    }
  }

  [Fact]
  public void Merge_FilesOfDifferentSessions_ShouldKeepThemAll()
  {
    var merged = ReportMerger.Merge(
      [
        CoverageReport.Create([FileCoverage.Create("a.cs", [Plain(1)])]),
        CoverageReport.Create([FileCoverage.Create("b.cs", [Plain(0)])]),
      ]);

    merged.Files.Select(file => file.Path).ShouldBe(["a.cs", "b.cs"]);
  }

  [Fact]
  public void Merge_NoReport_ShouldGiveEmptyReport()
  {
    ReportMerger.Merge([]).Files.ShouldBeEmpty();
  }

  private static LineCoverage MergeBothWays(LineCoverage first, LineCoverage second)
  {
    var merged = ReportMerger.MergeLine(first, second);

    ReportMerger.MergeLine(second, first).ShouldBe(merged);

    return merged;
  }

  private static LineCoverage Plain(long hits)
  {
    return new LineCoverage(1, hits, 0, 0, false, null);
  }

  private static LineCoverage Branch(int covered, int total, params string[] missing)
  {
    return new LineCoverage(1, 0, covered, total, false, missing.Length == 0 ? null : missing);
  }

  private static (string Path, LineCoverage Line)[] Flatten(CoverageReport report)
  {
    return [.. report.Files.SelectMany(file => file.Lines.Select(line => (file.Path, line)))];
  }

  private static CoverageReport RandomReport(Random random)
  {
    return CoverageReport.Create(
      Paths.Select(path => FileCoverage.Create(path, RandomLines(random))));
  }

  // Each session covers about a third of the lines, so a line is merged a varying number of times.
  private static LineCoverage[] RandomLines(Random random)
  {
    return
    [
      .. Enumerable.Range(1, 30)
        .Where(_ => random.Next(3) == 0)
        .Select(line => RandomLine(random, line)),
    ];
  }

  private static LineCoverage RandomLine(Random random, int line)
  {
    var isMethod = random.Next(5) == 0;

    if (random.Next(3) == 0)
    {
      return new LineCoverage(line, random.GetItems(PlainHits, 1)[0], 0, 0, isMethod, null);
    }

    var total = random.GetItems(BranchTotals, 1)[0];
    var covered = random.Next(total + 1);
    var missing = random.Next(2) == 0
      ? null
      : BranchIds.OrderBy(_ => random.Next()).Take(total - covered).ToArray();

    return new LineCoverage(line, random.Next(4), covered, total, isMethod, missing);
  }

  #endregion
}
