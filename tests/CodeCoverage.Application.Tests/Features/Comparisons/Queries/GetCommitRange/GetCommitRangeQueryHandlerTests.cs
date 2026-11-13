// ------------------------------------------------------------------------------------------------
// <copyright file="GetCommitRangeQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Comparisons.Queries.GetCommitRange;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

using static CodeCoverage.Application.Tests.Features.PullRequests.Queries.ComparisonFakes;

namespace CodeCoverage.Application.Tests.Features.Comparisons.Queries.GetCommitRange;

public sealed class GetCommitRangeQueryHandlerTests
{
  #region Fields

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IComparisonRepository _comparisons = A.Fake<IComparisonRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  #endregion

  #region Ctors

  public GetCommitRangeQueryHandlerTests()
  {
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(Repository());
    A.CallTo(() => _commits.FindByShaAsync(5, HeadSha, A<CancellationToken>._)).Returns(Commit(9, HeadSha, 3));
    A.CallTo(() => _commits.FindByShaAsync(5, BaseSha, A<CancellationToken>._)).Returns(Commit(8, BaseSha, 2));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The comparison of the push build is preferred, as CompareCommits serves it; nothing is computed or saved.
  [Fact]
  public async Task Handle_StoredPair_ShouldServeThePushBuildFirst()
  {
    var push = Stored(9, null);
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._)).Returns([Stored(9, 12), push]);

    var range = await Handler().Handle(Query(), Ct);

    range.Comparison.ShouldNotBeNull().Comparison.ShouldBeSameAs(push);
    range.Comparison.PullRequest.ShouldBeNull();
    A.CallTo(() => _comparisons.SaveAsync(A<CommitComparison>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Decision D4: a readable pair no notification compared answers « not computed », never « not found ».
  [Fact]
  public async Task Handle_PairNeverCompared_ShouldAnswerNotComputed()
  {
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._)).Returns([Stored(9, null) with { ComparedToSha = HeadSha }]);

    var range = await Handler().Handle(Query(), Ct);

    range.Comparison.ShouldBeNull();
    (range.Base, range.Head).ShouldBe((BaseSha, HeadSha));
  }

  [Fact]
  public async Task Handle_UnknownBase_ShouldThrowCommitNotFound()
  {
    A.CallTo(() => _commits.FindByShaAsync(5, BaseSha, A<CancellationToken>._)).Returns((Domain.Commits.Commit?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(() => Handler().Handle(Query(), Ct).AsTask());

    exception.ErrorCode.ShouldBe(CommitErrors.NotFound);
  }

  private static GetCommitRangeQuery Query() => new(Forge.GitHub, "octo-org", "app", BaseSha, HeadSha);

  private GetCommitRangeQueryHandler Handler() =>
    new(new CommitLocator(_repositories, _commits), _commits, _comparisons, new IndirectHeadTotalsReader(_reports, A.Fake<ICoverageReportCodec>()));

  #endregion
}
