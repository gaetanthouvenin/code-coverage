// ------------------------------------------------------------------------------------------------
// <copyright file="SweepPendingCommitsCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Features.Notifications.Commands.SweepPendingCommits;
using CodeCoverage.Application.Services;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Notifications.Commands.SweepPendingCommits;

public sealed class SweepPendingCommitsCommandHandlerTests
{
  #region Fields

  private const string ManualYaml = "manual";

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private readonly ICurrentOrganization _currentOrganization = A.Fake<ICurrentOrganization>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitConfigRepository _configs = A.Fake<ICommitConfigRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly ProcessingOptions _processing = new();

  private readonly List<JobRequest> _queued = [];

  private readonly SweepPendingCommitsCommandHandler _handler;

  #endregion

  #region Ctors

  public SweepPendingCommitsCommandHandlerTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    var parser = A.Fake<ICoverageConfigParser>();
    var settings = A.Fake<IOrganizationSettingsProvider>();
    var processing = A.Fake<IOptionsMonitor<ProcessingOptions>>();
    var clock = A.Fake<IClock>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => processing.CurrentValue).Returns(_processing);
    A.CallTo(() => gateways.GetGateway(A<Forge>._)).Returns(_gateway);
    A.CallTo(() => settings.GetAsync(A<CancellationToken>._)).Returns(new OrganizationSettingsSnapshot("coverage", true, true, null));
    A.CallTo(() => parser.Parse(ManualYaml))
     .Returns(new ConfigParseResult(ConfigNode.FromMap(("codecov", ConfigNode.FromMap(("notify", ConfigNode.FromMap(("manual_trigger", ConfigNode.FromBoolean(true))))))), null, []));

    // The configuration of a finalized commit is kept: the forge is never asked for it here.
    A.CallTo(() => _configs.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._))
     .ReturnsLazily((long repositoryId, GitSha sha, CancellationToken _) =>
                      Task.FromResult<CommitConfig?>(CommitConfig.Create(repositoryId, sha, ConfigSource.Builtin, null, null, [], [], Now)));
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, A<Commit?>._, A<int>._, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);
    A.CallTo(() => _jobQueue.TryEnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(true);

    _directory.Current = _directory.Add("1001", "octo", admitted: true);
    A.CallTo(() => _currentOrganization.IsKnown).Returns(true);

    _handler = new SweepPendingCommitsCommandHandler(
      _currentOrganization,
      _directory,
      _commits,
      _repositories,
      new ConfigResolver(_configs, gateways, parser, settings, clock),
      _jobQueue,
      processing,
      clock
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // A commit that waits for uploads that never come: notified by force once the timeout is over.
  [Fact]
  public async Task Handle_StaleNeverNotified_ShouldForceNotify()
  {
    Tracked(5);
    Stuck(Commit(9, 5), Commit(10, 5));

    await HandleAsync();

    _queued.Select(static j => j.PayloadJson).ShouldBe(["""{"CommitId":9,"Force":true,"Partial":true}""", """{"CommitId":10,"Force":true,"Partial":true}"""]);
    _queued.ShouldAllBe(static j => j.Type == JobTypes.NotifyCommit && j.Coalesce && j.OrganizationId == null && !j.IsInstance);
    _queued[0].DedupKey.ShouldBe("notify:9:sweep");
    _queued[0].LockKey.ShouldBe("commit:9");
    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(60)]
  [InlineData(15)]
  public async Task Handle_ShouldAskForTheCommitsWhoseLastUploadIsOlderThanTheTimeout(int timeoutMinutes)
  {
    _processing.PendingTimeoutMinutes = timeoutMinutes;

    await HandleAsync();

    // From the horizon to the timeout, with the clock of the application, and bounded.
    A.CallTo(() => _commits.ListStuckAsync(Now.AddDays(-7), Now.AddMinutes(-timeoutMinutes), null, SweepPendingCommitsCommandHandler.MaxCommits, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // Notified when its pipeline says so (uploads-complete), never by a timer.
  [Fact]
  public async Task Handle_RepositoryThatNotifiesOnDemand_ShouldBeLeftOut()
  {
    Tracked(5);
    Tracked(6);
    var manual = Commit(9, 5);
    A.CallTo(() => _configs.FindAsync(5, manual.Sha, A<CancellationToken>._))
     .Returns(CommitConfig.Create(5, manual.Sha, ConfigSource.Commit, "codecov.yml", ManualYaml, [], [], Now));
    Stuck(manual, Commit(10, 6));

    await HandleAsync();

    _queued.ShouldHaveSingleItem().PayloadJson.ShouldBe("""{"CommitId":10,"Force":true,"Partial":true}""");
  }

  // F5. The commits left out do not take the place of those after them: the sweep reads past them.
  [Fact]
  public async Task Handle_PageFullOfCommitsLeftOut_ShouldReadThePageAfterIt()
  {
    Tracked(5);
    Tracked(6);
    Commit[] manual = [.. Enumerable.Range(1, SweepPendingCommitsCommandHandler.MaxCommits).Select(static id => Commit(id, 5))];
    A.CallTo(() => _configs.FindAsync(5, A<GitSha>._, A<CancellationToken>._))
     .ReturnsLazily((long _, GitSha sha, CancellationToken _) => Task.FromResult<CommitConfig?>(CommitConfig.Create(5, sha, ConfigSource.Commit, "codecov.yml", ManualYaml, [], [], Now)));
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, null, A<int>._, A<CancellationToken>._)).Returns(manual);
    var last = manual[^1];
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, last, A<int>._, A<CancellationToken>._)).Returns([Commit(300, 6)]);

    await HandleAsync();

    _queued.ShouldHaveSingleItem().PayloadJson.ShouldBe("""{"CommitId":300,"Force":true,"Partial":true}""");
  }

  // S11. One sweep reads a bounded number of pages: the commits after them wait for a later sweep.
  [Fact]
  public async Task Handle_PagesFullOfCommitsLeftOut_ShouldStopAtTheBoundOfPages()
  {
    Tracked(5);
    A.CallTo(() => _configs.FindAsync(5, A<GitSha>._, A<CancellationToken>._))
     .ReturnsLazily((long _, GitSha sha, CancellationToken _) => Task.FromResult<CommitConfig?>(CommitConfig.Create(5, sha, ConfigSource.Commit, "codecov.yml", ManualYaml, [], [], Now)));
    var next = 1;
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, A<Commit?>._, A<int>._, A<CancellationToken>._))
     .ReturnsLazily(
       () =>
       {
         IReadOnlyList<Commit> page = [.. Enumerable.Range(next, SweepPendingCommitsCommandHandler.MaxCommits).Select(static id => Commit(id, 5))];
         next += SweepPendingCommitsCommandHandler.MaxCommits;

         return Task.FromResult(page);
       }
     );

    await HandleAsync();

    _queued.ShouldBeEmpty();
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, A<Commit?>._, A<int>._, A<CancellationToken>._))
     .MustHaveHappened(SweepPendingCommitsCommandHandler.MaxPages, Times.Exactly);
  }

  // The work of one sweep is bounded by the jobs it queues, whatever it reads.
  [Fact]
  public async Task Handle_MoreStuckCommitsThanTheBound_ShouldQueueTheBoundAndStop()
  {
    Tracked(5);
    Commit[] first = [.. Enumerable.Range(1, SweepPendingCommitsCommandHandler.MaxCommits).Select(static id => Commit(id, 5))];
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, null, A<int>._, A<CancellationToken>._)).Returns(first);
    var last = first[^1];
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, last, A<int>._, A<CancellationToken>._)).Returns([Commit(1000, 5)]);

    await HandleAsync();

    _queued.Count.ShouldBe(SweepPendingCommitsCommandHandler.MaxCommits);
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, A<Commit?>._, A<int>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // T1. The commits whose notification waits for its retry, refused by the forge for a while, take no place in the
  // bound: their job is already queued, and a newer stuck commit of another repository is still reached.
  [Fact]
  public async Task Handle_PageOfCommitsWaitingForTheirRetry_ShouldNotStarveANewerCommitOfAnotherRepository()
  {
    Tracked(5);
    Tracked(6);
    Commit[] refused = [.. Enumerable.Range(1, SweepPendingCommitsCommandHandler.MaxCommits).Select(static id => Commit(id, 5))];
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, null, A<int>._, A<CancellationToken>._)).Returns(refused);
    var last = refused[^1];
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, last, A<int>._, A<CancellationToken>._)).Returns([Commit(300, 6)]);
    // Their job, refused once, waits for its retry: the request merges into it.
    A.CallTo(() => _jobQueue.TryEnqueueAsync(A<JobRequest>.That.Matches(j => j.DedupKey != "notify:300:sweep"), A<CancellationToken>._))
     .Returns(false);

    await HandleAsync();

    A.CallTo(() => _jobQueue.TryEnqueueAsync(A<JobRequest>.That.Matches(j => j.PayloadJson == """{"CommitId":300,"Force":true,"Partial":true}"""), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _jobQueue.TryEnqueueAsync(A<JobRequest>._, A<CancellationToken>._)).MustHaveHappened(SweepPendingCommitsCommandHandler.MaxCommits + 1, Times.Exactly);
  }

  [Fact]
  public async Task Handle_RepositoryDisabled_ShouldBeLeftOut()
  {
    Tracked(5).Disable(Now);
    Stuck(Commit(9, 5));

    await HandleAsync();

    _queued.ShouldBeEmpty();
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Handle_RepositoryNoLongerTracked_ShouldBeLeftOut(bool known)
  {
    if (known)
    {
      Tracked(5).Deactivate(Now);
    }
    else
    {
      A.CallTo(() => _repositories.FindAsync(5, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    }

    Stuck(Commit(9, 5), Commit(10, 5));

    await HandleAsync();

    _queued.ShouldBeEmpty();
    // Read once for both commits.
    A.CallTo(() => _repositories.FindAsync(5, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_NothingStuck_ShouldQueueNothing()
  {
    await HandleAsync();

    _queued.ShouldBeEmpty();
    A.CallTo(_repositories).MustNotHaveHappened();
  }

  // The coordinator (ADR-013): on the lane of the instance it reads the organizations only, and queues one sweep per
  // active one, by its identifier.
  [Fact]
  public async Task Handle_OnTheLaneOfTheInstance_ShouldQueueOneSweepPerActiveOrganization()
  {
    A.CallTo(() => _currentOrganization.IsKnown).Returns(false);
    var second = _directory.Add("2002", "second", admitted: true);
    _directory.Add("3003", "pending");
    _directory.Add("4004", "suspended", admitted: true).Suspend(SuspensionReason.Administrator, Now);

    await HandleAsync();

    _queued.Select(static j => j.OrganizationId).ShouldBe([_directory.Current!.Id, second.Id]);
    _queued.ShouldAllBe(static j => j.Type == JobTypes.SweepPendingCommits
                                    && j.PayloadJson == "{}"
                                    && j.LockKey == "sweep"
                                    && j.DedupKey == "sweep"
                                    && j.Coalesce
                                    && !j.IsInstance);
    // No commit is read outside an organization.
    A.CallTo(_commits).MustNotHaveHappened();
    A.CallTo(_repositories).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private static Commit Commit(long id, long repositoryId)
  {
    var commit = Domain.Commits.Commit.Create(repositoryId, GitSha.Parse(id.ToString("x40", System.Globalization.CultureInfo.InvariantCulture)), Now.AddHours(-3));

    typeof(Commit).GetProperty(nameof(Domain.Commits.Commit.Id))!.SetValue(commit, id);

    return commit;
  }

  private async Task HandleAsync() => await _handler.Handle(new SweepPendingCommitsCommand(), Ct);

  private void Stuck(params Commit[] commits)
  {
    A.CallTo(() => _commits.ListStuckAsync(A<DateTimeOffset>._, A<DateTimeOffset>._, A<Commit?>._, A<int>._, A<CancellationToken>._)).Returns(commits);
  }

  private TrackedRepository Tracked(long id)
  {
    var repository = TrackedRepository.Create(42, id.ToString(System.Globalization.CultureInfo.InvariantCulture), RepositorySlug.Parse($"octo/repo{id}"), "main", false, "badge", Now);

    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, id);
    A.CallTo(() => _repositories.FindAsync(id, A<CancellationToken>._)).Returns(repository);

    return repository;
  }

  #endregion
}
