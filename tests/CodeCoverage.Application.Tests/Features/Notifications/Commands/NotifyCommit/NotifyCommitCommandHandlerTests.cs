// ------------------------------------------------------------------------------------------------
// <copyright file="NotifyCommitCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Notifications;
using CodeCoverage.Application.Features.Notifications.Commands.NotifyCommit;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Notifications.Commands.NotifyCommit;

public sealed class NotifyCommitCommandHandlerTests
{
  #region Fields

  private const string Yaml = "codecov: notify";

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly GitSha Head = GitSha.Parse(new string('b', GitSha.Length));

  private static readonly GitSha Base = GitSha.Parse(new string('c', GitSha.Length));

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitConfigRepository _configs = A.Fake<ICommitConfigRepository>();

  private readonly ICoverageConfigParser _configParser = A.Fake<ICoverageConfigParser>();

  private readonly IOrganizationSettingsProvider _settings = A.Fake<IOrganizationSettingsProvider>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly IPostedStatusRepository _posted = A.Fake<IPostedStatusRepository>();

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly INotificationMetrics _metrics = A.Fake<INotificationMetrics>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly List<JobRequest> _queued = [];

  private readonly Commit _commit = Commit.Create(5, Sha, Now.AddHours(-1));

  private readonly TrackedRepository _repository = TrackedRepository.Create(77, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);

  #endregion

  #region Ctors

  public NotifyCommitCommandHandlerTests()
  {
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_commit, 9L);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(_repository, 5L);

    // Received an upload 10 minutes ago; every upload failed, so that publishing needs no report.
    _commit.MarkPending("main", null, Now.AddMinutes(-10));
    _commit.RecordFinalization(CommitState.Error, 0, default, 0, 0, CarryforwardStatus.None, "source", Now.AddMinutes(-9));

    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(_commit);
    A.CallTo(() => _repositories.FindAsync(5, A<CancellationToken>._)).Returns(_repository);
    A.CallTo(() => _settings.GetAsync(A<CancellationToken>._)).Returns(new OrganizationSettingsSnapshot("coverage", true, true, null));
    A.CallTo(() => _posted.ListByCommitAsync(A<long>._, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);
    Notify(("notify_error", ConfigNode.FromBoolean(true)));
    Uploads(UploadState.Failed);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldPublishTheStatusesAndMarkTheCommitNotified()
  {
    await Handler().Handle(new NotifyCommitCommand(9), Ct);

    A.CallTo(() => _gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, Sha, A<CommitStatus>._, A<string?>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    _commit.NotifiedAt.ShouldBe(Now);
    A.CallTo(() => _commits.UpdateAsync(_commit, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    // From the last upload to the published status.
    A.CallTo(() => _metrics.NotifyLatency(TimeSpan.FromMinutes(10))).MustHaveHappenedOnceExactly();
  }

  // Nothing new to publish: the commit is still marked, and no latency is measured.
  [Fact]
  public async Task Handle_NothingPublished_ShouldMarkTheCommitWithoutMeasuringALatency()
  {
    A.CallTo(() => _settings.GetAsync(A<CancellationToken>._)).Returns(new OrganizationSettingsSnapshot("coverage", false, true, null));

    await Handler().Handle(new NotifyCommitCommand(9), Ct);

    A.CallTo(_gateway).MustNotHaveHappened();
    _commit.NotifiedAt.ShouldBe(Now);
    A.CallTo(_metrics).MustNotHaveHappened();
  }

  // The gate is asked again by the job: an upload arrived since the notification was queued.
  [Fact]
  public async Task Handle_UploadsInProgress_ShouldNotNotify()
  {
    Uploads(UploadState.Failed, UploadState.Received);

    await Handler().Handle(new NotifyCommitCommand(9), Ct);

    A.CallTo(_gateway).MustNotHaveHappened();
    _commit.NotifiedAt.ShouldBeNull();
    A.CallTo(() => _commits.UpdateAsync(A<Commit>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Asked by hand: it waits for the uploads, without using an attempt of the job.
  [Fact]
  public async Task Handle_ForcedWithUploadsInProgress_ShouldRetryLater()
  {
    Uploads(UploadState.Failed, UploadState.Processing);

    var exception = await Should.ThrowAsync<RetryLaterException>(async () => await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct));

    exception.Delay.ShouldBe(TimeSpan.FromSeconds(15));
    A.CallTo(_gateway).MustNotHaveHappened();
    _commit.NotifiedAt.ShouldBeNull();
  }

  // A commit already finalized stays complete when another upload ends: until its finalization runs again, its report
  // holds a part of what was sent. Asked by hand, the notification waits for that finalization instead of publishing
  // the statuses of the former report.
  [Fact]
  public async Task Handle_ForcedWithAnUploadProcessedSinceTheFinalization_ShouldRetryLater()
  {
    Uploads(UploadState.Failed, UploadState.Processed);

    var exception = await Should.ThrowAsync<RetryLaterException>(async () => await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct));

    exception.Delay.ShouldBe(TimeSpan.FromSeconds(15));
    A.CallTo(_gateway).MustNotHaveHappened();
    _commit.NotifiedAt.ShouldBeNull();
    A.CallTo(() => _commits.UpdateAsync(A<Commit>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Not asked by hand: the finalization to come queues its own notification.
  [Fact]
  public async Task Handle_UploadProcessedSinceTheFinalization_ShouldNotNotify()
  {
    Uploads(UploadState.Failed, UploadState.Processed);

    await Handler().Handle(new NotifyCommitCommand(9), Ct);

    A.CallTo(_gateway).MustNotHaveHappened();
    _commit.NotifiedAt.ShouldBeNull();
    A.CallTo(() => _commits.UpdateAsync(A<Commit>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // A stuck upload, or a finalization that never comes, does not make the job wait for ever: after the timeout of a
  // pending commit it gives up.
  [Theory]
  [InlineData(UploadState.Processing, 59, true)]
  [InlineData(UploadState.Processing, 60, false)]
  [InlineData(UploadState.Processed, 59, true)]
  [InlineData(UploadState.Processed, 60, false)]
  public async Task Handle_ForcedThatWaits_ShouldGiveUpAfterThePendingTimeout(UploadState state, int minutesSinceTheLastUpload, bool waits)
  {
    Uploads(state);
    A.CallTo(() => _clock.UtcNow).Returns(Now.AddMinutes(-10).AddMinutes(minutesSinceTheLastUpload));

    if (waits)
    {
      await Should.ThrowAsync<RetryLaterException>(async () => await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct));
    }
    else
    {
      await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct);
    }

    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(false, 0)]
  [InlineData(true, 2)]
  public async Task Handle_ManualTrigger_ShouldNotifyOnlyWhenForced(bool forced, int published)
  {
    Notify(("notify_error", ConfigNode.FromBoolean(true)), ("manual_trigger", ConfigNode.FromBoolean(true)));

    await Handler().Handle(new NotifyCommitCommand(9, forced), Ct);

    A.CallTo(() => _gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<CommitStatus>._, A<string?>._, A<CancellationToken>._)).MustHaveHappened(published, Times.Exactly);
    (_commit.NotifiedAt is not null).ShouldBe(forced);
  }

  [Theory]
  [InlineData(false, 0)]
  [InlineData(true, 2)]
  public async Task Handle_AfterNBuildsNotReached_ShouldNotifyOnlyWhenForced(bool forced, int published)
  {
    Notify(("notify_error", ConfigNode.FromBoolean(true)), ("after_n_builds", ConfigNode.FromNumber(3)));

    await Handler().Handle(new NotifyCommitCommand(9, forced), Ct);

    A.CallTo(() => _gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<CommitStatus>._, A<string?>._, A<CancellationToken>._)).MustHaveHappened(published, Times.Exactly);
  }

  // The uploads sent with the SHA of a synthetic merge commit are moved to the head of the pull request (§1.4-2):
  // asked by hand on that SHA, the notification is handed to the head, which holds them.
  [Fact]
  public async Task Handle_ForcedOnAMergeCommitWhoseUploadsWereMoved_ShouldHandTheNotificationToTheHead()
  {
    var merge = MergeCommit();
    Uploads();
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "title", null, Head, "feature", "main", Base, Sha));
    A.CallTo(() => _commits.FindByShaAsync(5, Head, A<CancellationToken>._)).Returns(HeadCommit());

    await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct);

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.NotifyCommit);
    job.PayloadJson.ShouldBe("""{"CommitId":10,"Force":true}""");
    job.LockKey.ShouldBe("commit:10");
    job.OrganizationId.ShouldBeNull();
    job.IsInstance.ShouldBeFalse();
    A.CallTo(() => _gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<CommitStatus>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    merge.NotifiedAt.ShouldBeNull();
    A.CallTo(() => _commits.UpdateAsync(A<Commit>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The uploader sent the upload again after the first copy was moved: the second copy is refused as a duplicate of
  // the head and stays on the merge commit, failed. The notification is still the one of the head.
  [Fact]
  public async Task Handle_ForcedOnAMergeCommitThatOnlyKeepsFailedUploads_ShouldHandTheNotificationToTheHead()
  {
    var merge = MergeCommit();
    merge.RecordFinalization(CommitState.Error, 0, default, 0, 0, CarryforwardStatus.None, "source", Now.AddMinutes(-9));
    Uploads(UploadState.Failed);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "title", null, Head, "feature", "main", Base, Sha));
    A.CallTo(() => _commits.FindByShaAsync(5, Head, A<CancellationToken>._)).Returns(HeadCommit());

    await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct);

    _queued.ShouldHaveSingleItem().PayloadJson.ShouldBe("""{"CommitId":10,"Force":true}""");
    A.CallTo(() => _gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<CommitStatus>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    merge.NotifiedAt.ShouldBeNull();
  }

  // The head of a pull request whose uploads all failed is no merge commit: its own error statuses are published.
  [Fact]
  public async Task Handle_ForcedOnTheHeadOfAPullRequestWhoseUploadsAllFailed_ShouldPublishItsErrors()
  {
    var head = MergeCommit();
    head.RecordFinalization(CommitState.Error, 0, default, 0, 0, CarryforwardStatus.None, "source", Now.AddMinutes(-9));
    Uploads(UploadState.Failed);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "title", null, Sha, "feature", "main", Base, Head));

    await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct);

    _queued.ShouldBeEmpty();
    A.CallTo(() => _gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, Sha, A<CommitStatus>._, A<string?>._, A<CancellationToken>._)).MustHaveHappened();
    head.NotifiedAt.ShouldBe(Now);
  }

  // The commit is the head of its pull request and has no upload: there is nothing to notify, and nothing to wait for.
  [Fact]
  public async Task Handle_ForcedOnTheHeadOfAPullRequestWithoutUpload_ShouldDoNothing()
  {
    MergeCommit();
    Uploads();
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "title", null, Sha, "feature", "main", Base, Head));

    await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct);

    _queued.ShouldBeEmpty();
    A.CallTo(() => _gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<CommitStatus>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The head never received anything here: no commit to hand the notification to.
  [Fact]
  public async Task Handle_ForcedOnAMergeCommitWhoseHeadIsUnknown_ShouldDoNothing()
  {
    MergeCommit();
    Uploads();
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "title", null, Head, "feature", "main", Base, Sha));
    A.CallTo(() => _commits.FindByShaAsync(5, Head, A<CancellationToken>._)).Returns((Commit?)null);

    await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct);

    _queued.ShouldBeEmpty();
  }

  // While the upload is still on the merge commit, the job waits for it as for any other: the move comes first.
  [Fact]
  public async Task Handle_ForcedOnAMergeCommitThatStillHoldsItsUpload_ShouldRetryLaterWithoutAskingTheForge()
  {
    MergeCommit();
    Uploads(UploadState.Received);

    await Should.ThrowAsync<RetryLaterException>(async () => await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct));

    _queued.ShouldBeEmpty();
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Only a notification asked by hand follows the uploads: the debounced one is queued by the finalization of the head.
  [Fact]
  public async Task Handle_NotForcedOnACommitWithoutUpload_ShouldNotAskTheForge()
  {
    MergeCommit();
    Uploads();

    await Handler().Handle(new NotifyCommitCommand(9), Ct);

    _queued.ShouldBeEmpty();
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_InactiveRepository_ShouldPublishNothing()
  {
    _repository.Deactivate(Now);

    await Handler().Handle(new NotifyCommitCommand(9, Force: true), Ct);

    A.CallTo(_gateway).MustNotHaveHappened();
    A.CallTo(_uploads).MustNotHaveHappened();
    _commit.NotifiedAt.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_CommitNotFinalizedYet_ShouldNotNotify()
  {
    var pending = Commit.Create(5, Sha, Now.AddMinutes(-10));
    pending.MarkPending("main", null, Now.AddMinutes(-10));
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(pending, 9L);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(pending);

    await Handler().Handle(new NotifyCommitCommand(9), Ct);

    A.CallTo(_gateway).MustNotHaveHappened();
    pending.NotifiedAt.ShouldBeNull();
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Handle_CommitOrRepositoryGone_ShouldThrowNotFound(bool commitGone)
  {
    if (commitGone)
    {
      A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns((Commit?)null);
    }
    else
    {
      A.CallTo(() => _repositories.FindAsync(5, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    }

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(new NotifyCommitCommand(9), Ct));

    exception.ErrorCode.ShouldBe(NotificationErrors.CommitNotFound);
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await Handler().Handle(null!, Ct));
  }

  private void Notify(params (string Key, ConfigNode Value)[] entries)
  {
    A.CallTo(() => _configs.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._))
     .Returns(CommitConfig.Create(5, Sha, ConfigSource.Commit, null, Yaml, [], [], Now));
    A.CallTo(() => _configParser.Parse(Yaml))
     .Returns(new ConfigParseResult(ConfigNode.FromMap(("codecov", ConfigNode.FromMap(("notify", ConfigNode.FromMap(entries))))), null, []));
  }

  private void Uploads(params UploadState[] states)
  {
    var uploads = states.Select(
                          static (state, index) =>
                          {
                            var upload = Upload.Receive(9, index + 1, $"key-{index}", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);

                            if (state is UploadState.Failed)
                            {
                              upload.MarkFailed("Upload.ParseFailed", "unreadable", 1, 0, 0, ProcessingLog.Empty, Now);
                            }
                            else if (state is UploadState.Processed)
                            {
                              upload.MarkProcessed(1, 1, 0, default, ProcessingLog.Empty, Now);
                            }
                            else if (state is UploadState.Processing)
                            {
                              typeof(Upload).GetProperty(nameof(Upload.State))!.SetValue(upload, UploadState.Processing);
                            }

                            return upload;
                          }
                        )
                        .ToList();

    A.CallTo(() => _uploads.ListByCommitAsync(9, A<CancellationToken>._)).Returns(uploads);
  }

  // The commit an upload named with the SHA of a merge commit: it received the upload, which was moved since.
  private Commit MergeCommit()
  {
    var merge = Commit.Create(5, Sha, Now.AddMinutes(-10));
    merge.MarkPending("feature", 12, Now.AddMinutes(-10));
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(merge, 9L);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(merge);

    return merge;
  }

  private static Commit HeadCommit()
  {
    var head = Commit.Create(5, Head, Now.AddMinutes(-10));
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(head, 10L);

    return head;
  }

  private NotifyCommitCommandHandler Handler()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    var directory = A.Fake<IOrganizationDirectory>();
    var views = A.Fake<IReportViewCache>();
    var urls = A.Fake<IOptionsMonitor<UrlOptions>>();
    var processing = A.Fake<IOptionsMonitor<ProcessingOptions>>();
    var notificationOptions = A.Fake<IOptionsMonitor<NotificationOptions>>();

    A.CallTo(() => gateways.GetGateway(A<Forge>._)).Returns(_gateway);
    A.CallTo(() => directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now));
    A.CallTo(() => urls.CurrentValue).Returns(new UrlOptions());
    A.CallTo(() => processing.CurrentValue).Returns(new ProcessingOptions());
    A.CallTo(() => notificationOptions.CurrentValue).Returns(new NotificationOptions());

    var resolver = new ConfigResolver(_configs, gateways, _configParser, _settings, _clock);
    var baseResolver = new BaseResolver(
      _pullRequests,
      _commits,
      new CommitAncestry(A.Fake<IGitCommitRepository>(), gateways, _clock),
      new DiffProvider(A.Fake<IDiffCacheRepository>(), gateways, _clock),
      gateways,
      processing,
      _clock
    );
    var builder = new ComparisonBuilder(baseResolver, new CommitReportReader(A.Fake<ICommitReportRepository>(), A.Fake<ICoverageReportCodec>()), views, A.Fake<IComparisonRepository>(), _clock);
    var commentOptions = A.Fake<IOptionsMonitor<NotificationOptions>>();
    A.CallTo(() => commentOptions.CurrentValue).Returns(new NotificationOptions());
    var comments = new CommentPublisher(gateways, _pullRequests, commentOptions, urls, _clock, NullLogger<CommentPublisher>.Instance);
    var service = new NotificationService(builder, views, _posted, A.Fake<IRepositoryFlagRepository>(), gateways, comments, _settings, urls, _clock, NullLogger<NotificationService>.Instance);

    var mergeCommits = new MergeCommitDetector(gateways, new CommitAncestry(A.Fake<IGitCommitRepository>(), gateways, _clock));

    return new NotifyCommitCommandHandler(
      _commits,
      _uploads,
      _repositories,
      directory,
      resolver,
      service,
      mergeCommits,
      _jobQueue,
      notificationOptions,
      processing,
      _metrics,
      _clock
    );
  }

  #endregion
}