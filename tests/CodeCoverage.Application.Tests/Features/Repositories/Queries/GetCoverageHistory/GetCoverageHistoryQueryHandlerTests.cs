// ------------------------------------------------------------------------------------------------
// <copyright file="GetCoverageHistoryQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Repositories.Queries.GetCoverageHistory;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Repositories.Queries.GetCoverageHistory;

public sealed class GetCoverageHistoryQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  // Monday 2 and Monday 9 November 2026 start two ISO weeks.
  private static readonly DateTimeOffset FirstMonday = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly IClock _clock = A.Fake<IClock>();

  #endregion

  #region Ctors

  public GetCoverageHistoryQueryHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _clock.UtcNow).Returns(Now);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_WeekInterval_ShouldKeepLastValuePerWeek()
  {
    Read(
      Entry(1, FirstMonday.AddHours(10), null, 60m),
      Entry(3, FirstMonday.AddDays(6).AddHours(23), null, 65m),
      Entry(2, FirstMonday.AddDays(3), null, 62m),
      Entry(4, FirstMonday.AddDays(7).AddHours(1), null, 70m)
    );

    var series = await Handler().Handle(Query(interval: "week", from: FirstMonday, to: Now), Ct);

    var points = series.ShouldHaveSingleItem().Points;
    points.Select(static p => p.At).ShouldBe([FirstMonday, FirstMonday.AddDays(7)]);
    points.Select(static p => p.Coverage).ShouldBe([65m, 70m]);
  }

  // The committer date orders and dates the points; a commit never read from the forge falls back on its finalization.
  [Fact]
  public async Task Handle_ShouldUseCommitterDateThenCompletedAt()
  {
    Read(
      Entry(1, Now.AddDays(-1), committedAt: Now.AddDays(-3), 60m),
      Entry(2, Now.AddDays(-2), committedAt: null, 70m)
    );

    var series = await Handler().Handle(Query(), Ct);

    var points = series.ShouldHaveSingleItem().Points;
    points.Select(static p => p.Coverage).ShouldBe([60m, 70m]);
    points.Select(static p => p.At).ShouldBe([Now.AddDays(-3), Now.AddDays(-2)]);
  }

  // Defaults: the default branch, the whole project, the last 30 days until now, the cap of 10 000.
  [Fact]
  public async Task Handle_WithoutOptions_ShouldReadTheProjectOfTheDefaultBranchOverThirtyDays()
  {
    Read();

    var series = await Handler().Handle(Query(), Ct);

    series.ShouldHaveSingleItem().Flag.ShouldBe(CommitFlagTotals.ProjectFlag);
    A.CallTo(() => _reports.ListHistoryAsync(
               5,
               "main",
               A<IReadOnlyCollection<string>>.That.IsSameSequenceAs(new[] { CommitFlagTotals.ProjectFlag }),
               Now.AddDays(-30),
               Now,
               10_000,
               A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // A repeated flag gives one series per flag (M3.22), once each.
  [Fact]
  public async Task Handle_SeveralFlags_ShouldGiveOneSeriesEach()
  {
    Read();

    var series = await Handler().Handle(Query(flags: ["unit", "e2e", "unit"], branch: "release"), Ct);

    series.Select(static s => s.Flag).ShouldBe(["unit", "e2e"]);
    A.CallTo(() => _reports.ListHistoryAsync(5, "release", A<IReadOnlyCollection<string>>.That.IsSameSequenceAs(new[] { "unit", "e2e" }), A<DateTimeOffset>._, A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("month")]
  [InlineData("Week")]
  public void Validate_UnknownInterval_ShouldFail(string interval) =>
    new GetCoverageHistoryQueryValidator().Validate(Query(interval: interval)).Errors.ShouldContain(static e => e.ErrorCode == "Repository.InvalidHistory");

  [Fact]
  public void Validate_FromAfterTo_ShouldFail() =>
    new GetCoverageHistoryQueryValidator().Validate(Query(from: Now, to: Now.AddDays(-1))).Errors.ShouldContain(static e => e.ErrorCode == "Repository.InvalidHistory");

  [Fact]
  public void Validate_TooManyFlags_ShouldFail() =>
    new GetCoverageHistoryQueryValidator().Validate(Query(flags: [.. Enumerable.Range(0, 21).Select(static i => $"f{i}")]))
                                           .Errors.ShouldContain(static e => e.ErrorCode == "Repository.InvalidHistory");

  private static HistoryEntry Entry(long commitId, DateTimeOffset completedAt, DateTimeOffset? committedAt, decimal coverage) =>
    new(commitId, GitSha.Parse(new string((char)('a' + commitId), GitSha.Length)), CommitFlagTotals.ProjectFlag, completedAt, committedAt, 10, 5, 5, 0, coverage);

  private static GetCoverageHistoryQuery Query(
    string interval = "commit",
    DateTimeOffset? from = null,
    DateTimeOffset? to = null,
    IReadOnlyList<string>? flags = null,
    string? branch = null) =>
    new(Forge.GitHub, "octo-org", "app", branch, flags ?? [], from, to, interval);

  private void Read(params HistoryEntry[] entries) =>
    A.CallTo(() => _reports.ListHistoryAsync(A<long>._, A<string>._, A<IReadOnlyCollection<string>>._, A<DateTimeOffset>._, A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
     .Returns(entries);

  private GetCoverageHistoryQueryHandler Handler() => new(new CommitLocator(_repositories, _commits), _reports, _clock);

  #endregion
}
