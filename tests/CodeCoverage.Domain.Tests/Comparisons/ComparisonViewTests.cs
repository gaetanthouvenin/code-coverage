// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonViewTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Tests.Comparisons;

public sealed class ComparisonViewTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha HeadSha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly GitSha BaseSha = GitSha.Parse(new string('b', GitSha.Length));

  #endregion

  #region Methods

  [Fact]
  public void Of_SameRevisions_ShouldNotBeStale()
  {
    var view = ComparisonView.Of(Reported(HeadSha, 3), Stored(3, BaseSha, 2), Reported(BaseSha, 2), null);

    view.IsStale.ShouldBeFalse();
    view.HeadSha.ShouldBe(HeadSha);
  }

  [Fact]
  public void Of_HeadReportChanged_ShouldBeStale() =>
    ComparisonView.Of(Reported(HeadSha, 4), Stored(3, BaseSha, 2), Reported(BaseSha, 2), null).IsStale.ShouldBeTrue();

  [Fact]
  public void Of_BaseReportChanged_ShouldBeStale() =>
    ComparisonView.Of(Reported(HeadSha, 3), Stored(3, BaseSha, 2), Reported(BaseSha, 5), null).IsStale.ShouldBeTrue();

  [Fact]
  public void Of_BaseGone_ShouldBeStale() =>
    ComparisonView.Of(Reported(HeadSha, 3), Stored(3, BaseSha, 2), null, null).IsStale.ShouldBeTrue();

  [Fact]
  public void Of_WithoutBase_ShouldOnlyWatchTheHead() =>
    ComparisonView.Of(Reported(HeadSha, 3), Stored(3, null, null), null, null).IsStale.ShouldBeFalse();

  private static Commit Reported(GitSha sha, int revision)
  {
    var commit = Commit.Create(5, sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.ReportRevision))!.SetValue(commit, revision);

    return commit;
  }

  private static CommitComparison Stored(int headRevision, GitSha? comparedTo, int? baseRevision) =>
    new(
      9,
      null,
      null,
      comparedTo,
      headRevision,
      baseRevision,
      DiffSource.Compare,
      new Comparison(new CoverageTotals(1, 1, 0, 0, 0), null, PatchCoverage.Empty, [], [], [], false),
      Now
    );

  #endregion
}
