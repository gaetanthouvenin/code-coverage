// ------------------------------------------------------------------------------------------------
// <copyright file="CompareCommitsQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Comparisons;
using CodeCoverage.Application.Features.Comparisons.Queries.CompareCommits;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

using static CodeCoverage.Application.Tests.Features.PullRequests.Queries.ComparisonFakes;

namespace CodeCoverage.Application.Tests.Features.Comparisons.Queries.CompareCommits;

public sealed class CompareCommitsQueryHandlerTests
{
  #region Fields

  private static readonly GitSha OtherBase = GitSha.Parse(new string('c', GitSha.Length));

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IComparisonRepository _comparisons = A.Fake<IComparisonRepository>();

  #endregion

  #region Ctors

  public CompareCommitsQueryHandlerTests()
  {
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(Repository());
    A.CallTo(() => _commits.FindByShaAsync(5, HeadSha, A<CancellationToken>._)).Returns(Commit(9, HeadSha, 3));
    A.CallTo(() => _commits.FindByShaAsync(5, BaseSha, A<CancellationToken>._)).Returns(Commit(8, BaseSha, 2));
    A.CallTo(() => _pullRequests.FindByNumberAsync(5, 12, A<CancellationToken>._)).Returns(Pull(12, HeadSha));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The push build comes first; nothing is computed or saved.
  [Fact]
  public async Task Handle_StoredAgainstTheBase_ShouldServeThePushBuildFirst()
  {
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._))
     .Returns([Stored(9, 12, headCoverage: 80m), Stored(9, null, headCoverage: 70m)]);

    var view = await Handler().Handle(Query(BaseSha), Ct);

    view.Comparison.PullNumber.ShouldBeNull();
    view.Comparison.Result.Head.Coverage.ShouldBe(70m);
    view.PullRequest.ShouldBeNull();
    view.IsStale.ShouldBeFalse();
    A.CallTo(() => _comparisons.SaveAsync(A<CommitComparison>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_OnlyAPullComparison_ShouldGiveItsPullRequest()
  {
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._)).Returns([Stored(9, 12)]);

    var view = await Handler().Handle(Query(BaseSha), Ct);

    view.PullRequest.ShouldNotBeNull().Number.ShouldBe(12);
  }

  // Another pair is never computed in a read.
  [Fact]
  public async Task Handle_StoredAgainstAnotherBase_ShouldThrowNotComputed()
  {
    A.CallTo(() => _commits.FindByShaAsync(5, OtherBase, A<CancellationToken>._)).Returns(Commit(7, OtherBase, 1));
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._)).Returns([Stored(9, null)]);

    var exception = await Should.ThrowAsync<NotFoundException>(() => Handler().Handle(Query(OtherBase), Ct).AsTask());

    exception.ErrorCode.ShouldBe(ComparisonErrors.NotComputed);
  }

  [Fact]
  public async Task Handle_UnknownBase_ShouldThrowCommitNotFound()
  {
    var exception = await Should.ThrowAsync<NotFoundException>(() => Handler().Handle(Query(OtherBase), Ct).AsTask());

    exception.ErrorCode.ShouldBe(CommitErrors.NotFound);
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private static CompareCommitsQuery Query(GitSha baseSha) => new(Forge.GitHub, "octo-org", "app", baseSha, HeadSha);

  private CompareCommitsQueryHandler Handler() => new(new CommitLocator(_repositories, _commits), _commits, _pullRequests, _comparisons);

  #endregion
}
