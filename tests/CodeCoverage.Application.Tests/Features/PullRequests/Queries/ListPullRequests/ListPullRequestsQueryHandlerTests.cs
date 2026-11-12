// ------------------------------------------------------------------------------------------------
// <copyright file="ListPullRequestsQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.PullRequests.Queries.ListPullRequests;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;

using static CodeCoverage.Application.Tests.Features.PullRequests.Queries.ComparisonFakes;

namespace CodeCoverage.Application.Tests.Features.PullRequests.Queries.ListPullRequests;

public sealed class ListPullRequestsQueryHandlerTests
{
  #region Fields

  private static readonly GitSha OtherHead = GitSha.Parse(new string('c', GitSha.Length));

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IComparisonRepository _comparisons = A.Fake<IComparisonRepository>();

  #endregion

  #region Ctors

  public ListPullRequestsQueryHandlerTests()
  {
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(Repository());
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // Each pull request gets the comparison of its own number on its head; a head never compared gets none.
  [Fact]
  public async Task Handle_ShouldJoinTheComparisonOfEachHead()
  {
    A.CallTo(() => _pullRequests.ListPageAsync(5, PullRequestState.Open, 1, 25, A<CancellationToken>._))
     .Returns(new PullRequestPage([Pull(12, HeadSha), Pull(13, HeadSha), Pull(14, OtherHead)], 1, 25, 3));
    A.CallTo(() => _commits.ListByShasAsync(5, A<IReadOnlyCollection<GitSha>>._, A<CancellationToken>._)).Returns([Commit(9, HeadSha, 3)]);
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>.That.IsSameSequenceAs(new long[] { 9 }), A<CancellationToken>._))
     .Returns([Stored(9, 12, headCoverage: 80m), Stored(9, null, headCoverage: 10m)]);

    var page = await Handler().Handle(new ListPullRequestsQuery(Forge.GitHub, "octo-org", "app"), Ct);

    page.TotalCount.ShouldBe(3);
    page.Items.Select(static i => i.PullRequest.Number).ShouldBe([12, 13, 14]);
    page.Items[0].Comparison.ShouldNotBeNull().Head.Coverage.ShouldBe(80m);
    page.Items[1].Comparison.ShouldBeNull();
    page.Items[2].Comparison.ShouldBeNull();
    A.CallTo(() => _commits.ListByShasAsync(5, A<IReadOnlyCollection<GitSha>>.That.Matches(s => s.Count == 2), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("open", PullRequestState.Open)]
  [InlineData("closed", PullRequestState.Closed)]
  [InlineData("merged", PullRequestState.Merged)]
  [InlineData("all", null)]
  public async Task Handle_State_ShouldFilterTheList(string state, PullRequestState? expected)
  {
    A.CallTo(() => _pullRequests.ListPageAsync(5, A<PullRequestState?>._, 2, 10, A<CancellationToken>._)).Returns(new PullRequestPage([], 2, 10, 0));

    await Handler().Handle(new ListPullRequestsQuery(Forge.GitHub, "octo-org", "app", state, 2, 10), Ct);

    A.CallTo(() => _pullRequests.ListPageAsync(5, expected, 2, 10, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("Open")]
  [InlineData("draft")]
  [InlineData("")]
  public void Validate_UnknownState_ShouldFail(string state)
  {
    var result = new ListPullRequestsQueryValidator().Validate(new ListPullRequestsQuery(Forge.GitHub, "octo-org", "app", state));

    result.Errors.ShouldContain(static e => e.ErrorCode == "PullRequest.InvalidState");
  }

  [Theory]
  [InlineData(0, 25)]
  [InlineData(1, 0)]
  [InlineData(1, 101)]
  [InlineData(21474838, 100)]
  public void Validate_PageOutOfBounds_ShouldFail(int page, int pageSize)
  {
    var result = new ListPullRequestsQueryValidator().Validate(new ListPullRequestsQuery(Forge.GitHub, "octo-org", "app", "open", page, pageSize));

    result.Errors.ShouldContain(static e => e.ErrorCode == "PullRequest.InvalidPage");
  }

  private ListPullRequestsQueryHandler Handler() => new(new CommitLocator(_repositories, _commits), _pullRequests, _commits, _comparisons);

  #endregion
}
