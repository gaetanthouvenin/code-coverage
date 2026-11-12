// ------------------------------------------------------------------------------------------------
// <copyright file="GetCommitQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Commits.Queries.GetCommit;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Commits.Queries.GetCommit;

public sealed class GetCommitQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IGitCommitRepository _gitCommits = A.Fake<IGitCommitRepository>();

  private readonly ICommitConfigRepository _configs = A.Fake<ICommitConfigRepository>();

  private readonly IComparisonRepository _comparisons = A.Fake<IComparisonRepository>();

  private readonly Commit _commit;

  #endregion

  #region Ctors

  public GetCommitQueryHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    _commit = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_commit, 9L);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns(_commit);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The page reads what was stored: the cached metadata, the stored configuration and the push comparison.
  [Fact]
  public async Task Handle_ShouldReadTheStoredMetadataConfigurationAndPushComparison()
  {
    var git = GitCommit.Create(5, new ForgeCommitInfo(Sha, [], "feat: retry", "octocat", Now), Now);
    var config = CommitConfig.Create(5, Sha, ConfigSource.Commit, "codecov.yml", null, [], [], Now);
    var comparison = new CommitComparison(9, null, null, null, 1, null, DiffSource.Compare, new Comparison(default, null, PatchCoverage.Empty, [], [], [], DiffIncomplete: false), Now);
    A.CallTo(() => _gitCommits.FindAsync(5, Sha, A<CancellationToken>._)).Returns(git);
    A.CallTo(() => _configs.FindAsync(5, Sha, A<CancellationToken>._)).Returns(config);
    A.CallTo(() => _comparisons.FindAsync(9, null, A<CancellationToken>._)).Returns(comparison);

    var detail = await Handler().Handle(new GetCommitQuery(Forge.GitHub, "octo-org", "app", Sha), Ct);

    detail.Git.ShouldBe(git);
    detail.Config.ShouldBe(config);
    detail.Comparison.ShouldBe(comparison);
  }

  // The Files card of the page gives the files of the base of the push comparison: read from its commit row.
  [Fact]
  public async Task Handle_PushComparisonWithBase_ShouldReadTheFilesOfTheBase()
  {
    var baseSha = GitSha.Parse(new string('b', GitSha.Length));
    var @base = Commit.Create(5, baseSha, Now);
    @base.RecordFinalization(CommitState.Complete, 210, new CoverageTotals(80, 20, 0, 0, 0), 1, 1, CarryforwardStatus.None, "default", Now);
    var comparison = new CommitComparison(9, null, null, baseSha, 1, 1, DiffSource.Compare, new Comparison(default, null, PatchCoverage.Empty, [], [], [], DiffIncomplete: false), Now);
    A.CallTo(() => _comparisons.FindAsync(9, null, A<CancellationToken>._)).Returns(comparison);
    A.CallTo(() => _commits.FindByShaAsync(5, baseSha, A<CancellationToken>._)).Returns(@base);

    var detail = await Handler().Handle(new GetCommitQuery(Forge.GitHub, "octo-org", "app", Sha), Ct);

    detail.BaseFiles.ShouldBe(210);
  }

  [Fact]
  public async Task Handle_NoComparison_ShouldHaveNoBaseFiles()
  {
    A.CallTo(() => _comparisons.FindAsync(9, null, A<CancellationToken>._)).Returns((CommitComparison?)null);

    var detail = await Handler().Handle(new GetCommitQuery(Forge.GitHub, "octo-org", "app", Sha), Ct);

    detail.BaseFiles.ShouldBeNull();
  }

  private GetCommitQueryHandler Handler() =>
    new(
      new CommitLocator(_repositories, _commits),
      A.Fake<ICommitReportRepository>(),
      A.Fake<IUploadRepository>(),
      A.Fake<IPostedStatusRepository>(),
      _gitCommits,
      _configs,
      _comparisons,
      _commits
    );

  #endregion
}
