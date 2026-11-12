// ------------------------------------------------------------------------------------------------
// <copyright file="GetPullRequestComparisonQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Comparisons;
using CodeCoverage.Application.Features.PullRequests;
using CodeCoverage.Application.Features.PullRequests.Queries.GetPullRequestComparison;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

using static CodeCoverage.Application.Tests.Features.PullRequests.Queries.ComparisonFakes;

namespace CodeCoverage.Application.Tests.Features.PullRequests.Queries.GetPullRequestComparison;

public sealed class GetPullRequestComparisonQueryHandlerTests
{
  #region Fields

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IComparisonRepository _comparisons = A.Fake<IComparisonRepository>();

  #endregion

  #region Ctors

  public GetPullRequestComparisonQueryHandlerTests()
  {
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(Repository());
    A.CallTo(() => _pullRequests.FindByNumberAsync(5, 12, A<CancellationToken>._)).Returns(Pull(12, HeadSha));
    A.CallTo(() => _commits.FindByShaAsync(5, HeadSha, A<CancellationToken>._)).Returns(Commit(9, HeadSha, 3));
    A.CallTo(() => _commits.FindByShaAsync(5, BaseSha, A<CancellationToken>._)).Returns(Commit(8, BaseSha, 2));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // Critical rule 3: a read serves what the notification stored; it computes, saves and queues nothing.
  [Fact]
  public async Task Handle_CachedComparison_ShouldNotRecompute()
  {
    var stored = Stored(9, 12);
    A.CallTo(() => _comparisons.FindAsync(9, 12, A<CancellationToken>._)).Returns(stored);

    var view = await Handler().Handle(Query(12), Ct);

    view.Comparison.ShouldBeSameAs(stored);
    view.HeadSha.ShouldBe(HeadSha);
    view.PullRequest.ShouldNotBeNull().Number.ShouldBe(12);
    view.IsStale.ShouldBeFalse();
    A.CallTo(() => _comparisons.SaveAsync(A<CommitComparison>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_pullRequests).Where(static c => c.Method.Name == nameof(IPullRequestRepository.UpdateAsync)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_HeadReportChangedSince_ShouldServeItAsStale()
  {
    A.CallTo(() => _comparisons.FindAsync(9, 12, A<CancellationToken>._)).Returns(Stored(9, 12, headRevision: 2));

    var view = await Handler().Handle(Query(12), Ct);

    view.IsStale.ShouldBeTrue();
    A.CallTo(() => _comparisons.SaveAsync(A<CommitComparison>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_NoComparison_ShouldThrowNotComputed()
  {
    A.CallTo(() => _comparisons.FindAsync(9, 12, A<CancellationToken>._)).Returns((CommitComparison?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(() => Handler().Handle(Query(12), Ct).AsTask());

    exception.ErrorCode.ShouldBe(ComparisonErrors.NotComputed);
  }

  // The head moved on the forge and its commit has no upload yet.
  [Fact]
  public async Task Handle_HeadCommitUnknown_ShouldThrowNotComputed()
  {
    A.CallTo(() => _commits.FindByShaAsync(5, HeadSha, A<CancellationToken>._)).Returns((Domain.Commits.Commit?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(() => Handler().Handle(Query(12), Ct).AsTask());

    exception.ErrorCode.ShouldBe(ComparisonErrors.NotComputed);
    A.CallTo(() => _comparisons.FindAsync(A<long>._, A<int?>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownNumber_ShouldThrowNotFound()
  {
    var exception = await Should.ThrowAsync<NotFoundException>(() => Handler().Handle(Query(13), Ct).AsTask());

    exception.ErrorCode.ShouldBe(PullRequestErrors.NotFound);
  }

  private static GetPullRequestComparisonQuery Query(int number) => new(Forge.GitHub, "octo-org", "app", number);

  private GetPullRequestComparisonQueryHandler Handler() => new(new CommitLocator(_repositories, _commits), _pullRequests, _commits, _comparisons);

  #endregion
}
