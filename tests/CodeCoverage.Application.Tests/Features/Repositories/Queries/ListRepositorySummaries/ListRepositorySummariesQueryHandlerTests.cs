// ------------------------------------------------------------------------------------------------
// <copyright file="ListRepositorySummariesQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories.Queries.ListRepositorySummaries;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Repositories.Queries.ListRepositorySummaries;

public sealed class ListRepositorySummariesQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ICurrentUser _currentUser = A.Fake<ICurrentUser>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IRepositoryFlagRepository _flags = A.Fake<IRepositoryFlagRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly ListRepositorySummariesQueryHandler _handler;

  #endregion

  #region Ctors

  public ListRepositorySummariesQueryHandlerTests()
  {
    A.CallTo(() => _currentUser.UserId).Returns(42L);
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    _handler = new ListRepositorySummariesQueryHandler(_currentUser, _repositories, _access, _commits, _flags, _reports, _clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The readable filter comes first: the grouped reads are asked for the readable repositories only, once each.
  [Fact]
  public async Task Handle_ShouldReadTheSummariesOfTheReadableRepositoriesInGroupedReads()
  {
    var readable = Repository(5, "100", "octo-org/app");
    var hidden = Repository(6, "200", "octo-org/secret");
    var head = Commit(5, 'a', Now.AddDays(-1).AddHours(2));
    A.CallTo(() => _repositories.SearchActiveAsync(null, A<CancellationToken>._)).Returns([readable, hidden]);
    A.CallTo(() => _access.ListReadableAsync(42, A<CancellationToken>._)).Returns(new HashSet<string>(["100"], StringComparer.Ordinal));
    A.CallTo(() => _commits.ListCoveredDefaultHeadsAsync(A<IReadOnlyCollection<long>>.That.IsSameSequenceAs(new[] { 5L }), A<CancellationToken>._)).Returns([head]);
    A.CallTo(() => _flags.ListByRepositoriesAsync(A<IReadOnlyCollection<long>>.That.IsSameSequenceAs(new[] { 5L }), A<CancellationToken>._))
     .Returns(new Dictionary<long, IReadOnlyList<string>> { [5] = ["backend"] });
    A.CallTo(() => _reports.ListDefaultBranchHistoryAsync(A<IReadOnlyCollection<long>>.That.IsSameSequenceAs(new[] { 5L }), Now.AddDays(-30), Now, ListRepositorySummariesQuery.MaxHistoryRows, A<CancellationToken>._))
     .Returns(
      [
        Totals(Commit(5, 'c', Now.AddDays(-1).AddHours(2)), 82m),
        Totals(Commit(5, 'b', Now.AddDays(-1).AddHours(1)), 81m),
        Totals(Commit(5, 'd', Now.AddDays(-3)), 79m),
      ]);

    var summaries = await _handler.Handle(new ListRepositorySummariesQuery(Forge.GitHub, "octo-org", null), Ct);

    var summary = summaries.ShouldHaveSingleItem();
    summary.Repository.ShouldBe(readable);
    summary.CoveredHead.ShouldBe(head);
    summary.Flags.ShouldBe(["backend"]);
    summary.History.Select(static d => (d.Day, d.Coverage)).ShouldBe([(Day(Now.AddDays(-3)), 79m), (Day(Now.AddDays(-1)), 82m)]);
  }

  [Fact]
  public async Task Handle_NothingReadable_ShouldReadNothingElse()
  {
    A.CallTo(() => _repositories.SearchActiveAsync(null, A<CancellationToken>._)).Returns([Repository(5, "100", "octo-org/app")]);
    A.CallTo(() => _access.ListReadableAsync(42, A<CancellationToken>._)).Returns(new HashSet<string>(StringComparer.Ordinal));

    (await _handler.Handle(new ListRepositorySummariesQuery(Forge.GitHub, "octo-org", null), Ct)).ShouldBeEmpty();

    A.CallTo(() => _commits.ListCoveredDefaultHeadsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _reports.ListDefaultBranchHistoryAsync(A<IReadOnlyCollection<long>>._, A<DateTimeOffset>._, A<DateTimeOffset>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_NoUser_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _currentUser.UserId).Returns(null);

    await Should.ThrowAsync<UnauthorizedException>(async () => await _handler.Handle(new ListRepositorySummariesQuery(Forge.GitHub, "octo-org", null), Ct));
  }

  private static DateTimeOffset Day(DateTimeOffset at) => new(at.Year, at.Month, at.Day, 0, 0, 0, TimeSpan.Zero);

  private static TrackedRepository Repository(long id, string externalId, string slug)
  {
    var repository = TrackedRepository.Create(1, externalId, RepositorySlug.Parse(slug), "main", false, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, id);

    return repository;
  }

  private static Commit Commit(long repositoryId, char sha, DateTimeOffset completedAt)
  {
    var commit = Domain.Commits.Commit.Create(repositoryId, GitSha.Parse(new string(sha, GitSha.Length)), completedAt);
    commit.MarkPending("main", null, completedAt);
    commit.RecordFinalization(CommitState.Complete, 1, new CoverageTotals(8, 2, 0, 0, 0), 1, 1, CarryforwardStatus.None, "default", completedAt);

    return commit;
  }

  private static CommitFlagTotals Totals(Commit commit, decimal coverage)
  {
    var hits = (int)coverage;

    return CommitFlagTotals.Create(commit, CommitFlagTotals.ProjectFlag, new CoverageTotals(hits, 100 - hits, 0, 0, 0), false, null);
  }

  #endregion
}
