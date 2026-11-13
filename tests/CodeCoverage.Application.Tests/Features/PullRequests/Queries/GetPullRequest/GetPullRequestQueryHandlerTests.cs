// ------------------------------------------------------------------------------------------------
// <copyright file="GetPullRequestQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.PullRequests;
using CodeCoverage.Application.Features.PullRequests.Queries.GetPullRequest;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

using static CodeCoverage.Application.Tests.Features.PullRequests.Queries.ComparisonFakes;

namespace CodeCoverage.Application.Tests.Features.PullRequests.Queries.GetPullRequest;

public sealed class GetPullRequestQueryHandlerTests
{
  #region Fields

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IComparisonRepository _comparisons = A.Fake<IComparisonRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly ICoverageReportCodec _codec = A.Fake<ICoverageReportCodec>();

  #endregion

  #region Ctors

  public GetPullRequestQueryHandlerTests()
  {
    var head = Commit(9, HeadSha, 3);
    typeof(Domain.Commits.Commit).GetProperty(nameof(Domain.Commits.Commit.Files))!.SetValue(head, 212);
    var @base = Commit(8, BaseSha, 2);
    typeof(Domain.Commits.Commit).GetProperty(nameof(Domain.Commits.Commit.Files))!.SetValue(@base, 210);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(Repository());
    A.CallTo(() => _pullRequests.FindByNumberAsync(5, 12, A<CancellationToken>._)).Returns(Pull(12, HeadSha));
    A.CallTo(() => _commits.FindByShaAsync(5, HeadSha, A<CancellationToken>._)).Returns(head);
    A.CallTo(() => _commits.FindByShaAsync(5, BaseSha, A<CancellationToken>._)).Returns(@base);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // Critical rule 3: the page serves what a notification stored, with the file counts of both rows.
  [Fact]
  public async Task Handle_StoredComparison_ShouldServeItWithTheFileCounts()
  {
    var stored = Stored(9, 12) with { BehindBy = 3, Hops = 1 };
    A.CallTo(() => _comparisons.FindAsync(9, 12, A<CancellationToken>._)).Returns(stored);

    var detail = await Handler().Handle(Query(12), Ct);

    detail.PullRequest.Number.ShouldBe(12);
    var view = detail.Comparison.ShouldNotBeNull();
    view.Comparison.ShouldBeSameAs(stored);
    (view.HeadFiles, view.BaseFiles).ShouldBe((212, 210));
    A.CallTo(() => _comparisons.SaveAsync(A<CommitComparison>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The head has no comparison yet: the pull request is shown, its comparison pending, never « not found ».
  [Fact]
  public async Task Handle_NoComparison_ShouldAnswerPending()
  {
    A.CallTo(() => _comparisons.FindAsync(9, 12, A<CancellationToken>._)).Returns((CommitComparison?)null);

    var detail = await Handler().Handle(Query(12), Ct);

    detail.PullRequest.Title.ShouldBe("Add a feature");
    detail.Comparison.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_HeadCommitUnknown_ShouldAnswerPendingWithoutReadingComparisons()
  {
    A.CallTo(() => _commits.FindByShaAsync(5, HeadSha, A<CancellationToken>._)).Returns((Domain.Commits.Commit?)null);

    var detail = await Handler().Handle(Query(12), Ct);

    detail.Comparison.ShouldBeNull();
    A.CallTo(() => _comparisons.FindAsync(A<long>._, A<int?>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownNumber_ShouldThrowNotFound()
  {
    var exception = await Should.ThrowAsync<NotFoundException>(() => Handler().Handle(Query(13), Ct).AsTask());

    exception.ErrorCode.ShouldBe(PullRequestErrors.NotFound);
  }

  // Without a base report, the base has no file count: the diff table shows its column missing.
  [Fact]
  public async Task Handle_BaseMissing_ShouldGiveNoBaseFiles()
  {
    var stored = Stored(9, 12) with { Result = Stored(9, 12).Result with { Base = null } };
    A.CallTo(() => _comparisons.FindAsync(9, 12, A<CancellationToken>._)).Returns(stored);

    var view = (await Handler().Handle(Query(12), Ct)).Comparison.ShouldNotBeNull();

    view.BaseFiles.ShouldBeNull();
    view.HeadFiles.ShouldBe(212);
  }

  // The head coverage of an indirect change is read at read time from the file index of the head: no blob change.
  [Fact]
  public async Task Handle_IndirectChanges_ShouldReadTheirHeadTotals()
  {
    var result = Stored(9, 12).Result with { IndirectChanges = [new IndirectChange("src/b.cs", IndirectChangeKind.Changed, 1, -1, 0, 5m)] };
    A.CallTo(() => _comparisons.FindAsync(9, 12, A<CancellationToken>._)).Returns(Stored(9, 12) with { Result = result });
    A.CallTo(() => _reports.GetFileIndexAsync(9, A<CancellationToken>._)).Returns(new byte[] { 1 });
    A.CallTo(() => _codec.DecodeFileIndex(A<ReadOnlyMemory<byte>>._))
     .Returns([new FileTotals("src/a.cs", new CoverageTotals(1, 1, 0, 0, 0)), new FileTotals("src/b.cs", new CoverageTotals(3, 1, 0, 0, 0))]);

    var detail = await Handler().Handle(Query(12), Ct);

    detail.IndirectHeadTotals.ShouldNotBeNull().Keys.ShouldBe(["src/b.cs"]);
    detail.IndirectHeadTotals["src/b.cs"].Coverage.ShouldBe(75m);
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._)).MustNotHaveHappened();
  }

  private static GetPullRequestQuery Query(int number) => new(Forge.GitHub, "octo-org", "app", number);

  private GetPullRequestQueryHandler Handler() =>
    new(new CommitLocator(_repositories, _commits), _pullRequests, _commits, _comparisons, new IndirectHeadTotalsReader(_reports, _codec));

  #endregion
}
