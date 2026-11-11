// ------------------------------------------------------------------------------------------------
// <copyright file="CommentSamples.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Application.Services.Comments;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Services.Comments;

// The pull request of ARCHITECTURE §8.4, from which every test of the comment starts and changes one thing.
internal static class CommentSamples
{
  #region Fields

  public const long RepositoryId = 5;

  public static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  public static readonly GitSha HeadSha = GitSha.Parse("e4f5a6b" + new string('0', 33));

  public static readonly GitSha BaseSha = GitSha.Parse("a1b2c3d" + new string('0', 33));

  public static readonly GitSha OlderSha = GitSha.Parse("9f8e7d6" + new string('0', 33));

  public static readonly CoverageTotals HeadTotals = new(12266, 2480, 354, 3010, 0);

  public static readonly CoverageTotals BaseTotals = new(12135, 2500, 365, 3000, 0);

  public static readonly CoverageTotals PatchTotals = new(29, 9, 2, 0, 0);

  #endregion

  #region Methods

  public static CoverageConfig Config(params (string Key, ConfigNode Value)[] comment) =>
    CoverageConfig.Create(ConfigMerger.Merge(CoverageConfigDefaults.Root, ConfigNode.FromMap(("comment", ConfigNode.FromMap(comment)))));

  public static CoverageConfig Layout(string layout) => Config(("layout", ConfigNode.FromText(layout)));

  public static WebLinks Links(string? web = "https://coverage.example.com") =>
    new(web, Forge.GitHub, TrackedRepository.Create(42, "7", RepositorySlug.Parse("octo/repo"), "main", false, "badge", Now));

  public static Commit Commit(GitSha sha, long id)
  {
    var commit = Domain.Commits.Commit.Create(RepositoryId, sha, Now);

    typeof(Commit).GetProperty(nameof(Domain.Commits.Commit.Id))!.SetValue(commit, id);

    return commit;
  }

  public static PullRequest Pull(string baseBranch = "main") =>
    PullRequest.Create(RepositoryId, new ForgePullInfo(123, ForgePullState.Open, "Add a feature", "octocat", HeadSha, "feature/x", baseBranch, BaseSha), Now);

  public static Comparison Comparison(
    CoverageTotals? head = null,
    CoverageTotals? @base = null,
    CoverageTotals? patch = null,
    bool withoutBase = false,
    bool diffIncomplete = false,
    IReadOnlyList<ImpactedFile>? files = null,
    IReadOnlyList<FlagComparison>? flags = null,
    IReadOnlyList<IndirectChange>? indirect = null) =>
    new(
      head ?? HeadTotals,
      withoutBase ? null : @base ?? BaseTotals,
      new PatchCoverage(patch ?? PatchTotals, []),
      files ?? [],
      flags ?? [],
      indirect ?? [],
      diffIncomplete
    );

  // comparedTo is the commit whose report is compared; hops counts the commits between it and the fork point.
  public static CommentContent Content(
    CoverageConfig? config = null,
    Comparison? comparison = null,
    GitSha? comparedTo = null,
    bool withoutBase = false,
    int? hops = 0,
    int? behindBy = 0,
    WebLinks? links = null,
    PullRequest? pull = null,
    IReadOnlyList<SessionCoverage>? sessions = null,
    int failedUploads = 0,
    bool wrapDetails = true,
    bool configInvalid = false,
    string title = "Coverage")
  {
    pull ??= Pull();
    comparison ??= Comparison(withoutBase: withoutBase);

    var compared = withoutBase ? null : comparedTo ?? BaseSha;
    var resolution = new BaseResolution(
      pull,
      BaseSha,
      compared is null ? null : Commit(compared, 8),
      BaseResolution.NoDiff(),
      BaseResolution.NoDiff(),
      behindBy,
      withoutBase ? null : hops
    );
    var stored = new CommitComparison(9, pull.Number, BaseSha, compared, 1, withoutBase ? null : 1, DiffSource.PullFiles, comparison, Now);

    return new CommentContent(
      CommentComposer.Marker,
      title,
      config ?? Config(),
      Commit(HeadSha, 9),
      pull,
      new ComparisonContext(stored, resolution, sessions ?? [], null),
      212,
      withoutBase ? null : 210,
      failedUploads,
      links ?? Links(),
      wrapDetails,
      configInvalid
    );
  }

  #endregion
}
