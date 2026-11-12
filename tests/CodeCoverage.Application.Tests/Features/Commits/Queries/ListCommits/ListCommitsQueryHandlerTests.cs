// ------------------------------------------------------------------------------------------------
// <copyright file="ListCommitsQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Commits.Queries.ListCommits;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Commits.Queries.ListCommits;

public sealed class ListCommitsQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha First = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly GitSha Second = GitSha.Parse(new string('b', GitSha.Length));

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IGitCommitRepository _gitCommits = A.Fake<IGitCommitRepository>();

  private readonly IComparisonRepository _comparisons = A.Fake<IComparisonRepository>();

  private readonly Commit _first;

  private readonly Commit _second;

  #endregion

  #region Ctors

  public ListCommitsQueryHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    _first = Commit.Create(5, First, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_first, 9L);
    _second = Commit.Create(5, Second, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_second, 10L);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _commits.ListPageAsync(5, "main", 2, 25, A<CancellationToken>._)).Returns(new CommitPage([_first, _second], 2, 25, 27));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The metadata come from the cache, the delta from the push comparison: a pull request comparison is not the one listed.
  [Fact]
  public async Task Handle_ShouldJoinTheCachedMetadataAndThePushDelta()
  {
    A.CallTo(() => _gitCommits.ListByShasAsync(5, A<IReadOnlyCollection<GitSha>>.That.IsSameSequenceAs(new[] { First, Second }), A<CancellationToken>._))
     .Returns([GitCommit.Create(5, new ForgeCommitInfo(First, [], "feat: retry", "octocat", Now), Now)]);
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>.That.IsSameSequenceAs(new[] { 9L, 10L }), A<CancellationToken>._))
     .Returns([Comparison(9, null, new CoverageTotals(81, 19, 0, 0, 0)), Comparison(9, 423, new CoverageTotals(50, 50, 0, 0, 0))]);

    var page = await Handler().Handle(new ListCommitsQuery(Forge.GitHub, "octo-org", "app", "main", 2), Ct);

    (page.Page, page.PageSize, page.TotalCount).ShouldBe((2, 25, 27));
    page.Items.Select(static i => i.Commit).ShouldBe([_first, _second]);
    page.Items[0].Git.ShouldNotBeNull().Message.ShouldBe("feat: retry");
    page.Items[0].Delta.ShouldBe(1m);
    page.Items[1].Git.ShouldBeNull();
    page.Items[1].Delta.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_EmptyPage_ShouldReadNothingElse()
  {
    A.CallTo(() => _commits.ListPageAsync(5, null, 1, 25, A<CancellationToken>._)).Returns(new CommitPage([], 1, 25, 0));

    var page = await Handler().Handle(new ListCommitsQuery(Forge.GitHub, "octo-org", "app", null), Ct);

    page.Items.ShouldBeEmpty();
    A.CallTo(() => _gitCommits.ListByShasAsync(A<long>._, A<IReadOnlyCollection<GitSha>>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The base had 80 % coverage: the head at 81 % gives +1 point.
  private static CommitComparison Comparison(long commitId, int? pullNumber, CoverageTotals head) =>
    new(
      commitId,
      pullNumber,
      null,
      null,
      1,
      1,
      DiffSource.Compare,
      new Comparison(head, new CoverageTotals(80, 20, 0, 0, 0), PatchCoverage.Empty, [], [], [], DiffIncomplete: false),
      Now
    );

  private ListCommitsQueryHandler Handler() => new(new CommitLocator(_repositories, _commits), _commits, _gitCommits, _comparisons);

  #endregion
}
