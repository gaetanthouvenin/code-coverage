// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonFakes.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.PullRequests.Queries;

// The rows the reads of the pull requests and of the comparisons work on.
internal static class ComparisonFakes
{
  #region Fields

  public static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  public static readonly GitSha HeadSha = GitSha.Parse(new string('a', GitSha.Length));

  public static readonly GitSha BaseSha = GitSha.Parse(new string('b', GitSha.Length));

  #endregion

  #region Methods

  public static TrackedRepository Repository()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    return repository;
  }

  public static Commit Commit(long id, GitSha sha, int revision)
  {
    var commit = Domain.Commits.Commit.Create(5, sha, Now);
    typeof(Commit).GetProperty(nameof(Domain.Commits.Commit.Id))!.SetValue(commit, id);
    typeof(Commit).GetProperty(nameof(Domain.Commits.Commit.ReportRevision))!.SetValue(commit, revision);

    return commit;
  }

  public static PullRequest Pull(int number, GitSha head) =>
    PullRequest.Create(5, new ForgePullInfo(number, ForgePullState.Open, "Add a feature", "octocat", head, "feature", "main", BaseSha), Now);

  public static CommitComparison Stored(long commitId, int? pullNumber, int headRevision = 3, decimal headCoverage = 75m) =>
    new(
      commitId,
      pullNumber,
      BaseSha,
      BaseSha,
      headRevision,
      2,
      DiffSource.PullFiles,
      new Comparison(Totals(headCoverage), Totals(50m), PatchCoverage.Empty, [], [], [], DiffIncomplete: true),
      Now
    );

  private static CoverageTotals Totals(decimal coverage) => new((int)coverage, 100 - (int)coverage, 0, 0, 0);

  #endregion
}
