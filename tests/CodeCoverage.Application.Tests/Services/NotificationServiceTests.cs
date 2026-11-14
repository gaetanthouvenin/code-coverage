// ------------------------------------------------------------------------------------------------
// <copyright file="NotificationServiceTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Services;

public sealed class NotificationServiceTests
{
  #region Fields

  private const long HeadId = 100;

  private const long BaseId = 200;

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Head = GitSha.Parse("aaaaaaa1" + new string('0', 32));

  private static readonly GitSha Parent = GitSha.Parse("bbbbbbb2" + new string('0', 32));

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IGitCommitRepository _gitCommits = A.Fake<IGitCommitRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly ICoverageReportCodec _codec = A.Fake<ICoverageReportCodec>();

  private readonly IPostedStatusRepository _posted = A.Fake<IPostedStatusRepository>();

  private readonly IRepositoryFlagRepository _repositoryFlags = A.Fake<IRepositoryFlagRepository>();

  private readonly IOrganizationSettingsProvider _settings = A.Fake<IOrganizationSettingsProvider>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly UrlOptions _urls = new() { WebPublicUrl = "https://coverage.example.com/" };

  private readonly Dictionary<byte, CoverageReport> _blobs = [];

  private readonly List<PostedStatus> _stored = [];

  private readonly List<(GitSha Sha, CommitStatus Status, string? Url)> _published = [];

  private readonly IGitHubAppGateway _gitHub = A.Fake<IGitHubAppGateway>();

  private readonly List<GitHubCheckRun> _checkRuns = [];

  private readonly TrackedRepository _repository = TrackedRepository.Create(42, "7", RepositorySlug.Parse("octo/app"), "main", false, "badge", Now);

  private readonly NotificationService _service;

  #endregion

  #region Ctors

  public NotificationServiceTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    var clock = A.Fake<IClock>();
    var processing = A.Fake<IOptionsMonitor<ProcessingOptions>>();
    var urls = A.Fake<IOptionsMonitor<UrlOptions>>();
    var diffCache = A.Fake<IDiffCacheRepository>();
    var views = A.Fake<IReportViewCache>();

    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => processing.CurrentValue).Returns(new ProcessingOptions());
    A.CallTo(() => urls.CurrentValue).Returns(_urls);
    A.CallTo(() => diffCache.FindAsync(A<long>._, A<GitSha>._, A<GitSha>._, A<DiffSource>._, A<CancellationToken>._)).Returns((DiffSet?)null);
    A.CallTo(() => views.GetOrCreate(A<long>._, A<int>._, A<IReadOnlyList<SessionReport>>._, A<IReadOnlyCollection<string>>._, A<PathPatternSet>._))
     .ReturnsLazily((long _, int _, IReadOnlyList<SessionReport> sessions, IReadOnlyCollection<string> flags, PathPatternSet paths) => ReportView.Build(sessions, flags, paths));
    A.CallTo(() => _pullRequests.FindOpenByHeadShaAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns((PullRequest?)null);
    A.CallTo(() => _commits.FindByShaAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns((Commit?)null);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).Returns((ForgePullInfo?)null);
    A.CallTo(() => _gateway.CompareAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<GitSha>._, A<CancellationToken>._))
     .Returns(new ForgeCompareResult(Parent, 1, 0, [new ForgeChangedFile("src/a.cs", null, ForgeChangedFileStatus.Modified, "@@ -1,2 +1,4 @@\n a\n+x\n+y\n b")], Truncated: false));
    A.CallTo(() => _gateway.ListCommitsAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<int>._, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => _gitCommits.FindAsync(A<long>._, Head, A<CancellationToken>._))
     .Returns(GitCommit.Create(1, new ForgeCommitInfo(Head, [Parent], "message", null, Now), Now));
    A.CallTo(() => _gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<CommitStatus>._, A<string?>._, A<CancellationToken>._))
     .Invokes((ForgeRepositoryRef _, GitSha sha, CommitStatus status, string? url, CancellationToken _) => _published.Add((sha, status, url)));
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._)).ReturnsLazily((ReadOnlyMemory<byte> blob) => _blobs[blob.Span[0]]);
    A.CallTo(() => _posted.ListByCommitAsync(HeadId, A<CancellationToken>._)).ReturnsLazily(() => (IReadOnlyList<PostedStatus>)[.. _stored]);
    A.CallTo(() => _posted.UpsertAsync(A<PostedStatus>._, A<CancellationToken>._))
     .Invokes(
       (PostedStatus status, CancellationToken _) =>
       {
         _stored.RemoveAll(s => s.Status.Context == status.Status.Context);
         _stored.Add(status);
       }
     );
    Settings("coverage", enabled: true);

    // Head: 2 hits on 4 lines (50%), its patch 1 hit on 2 lines. Base: 3 hits on 4 lines (75%).
    Sessions(HeadId, 1, Report(File("src/a.cs", (1, 1), (2, 1), (3, 0), (4, 0))));
    Sessions(BaseId, 2, Report(File("src/a.cs", (1, 1), (2, 1), (3, 1), (4, 0))));
    A.CallTo(() => _commits.FindByShaAsync(A<long>._, Parent, A<CancellationToken>._)).Returns(Complete(BaseId, Parent));

    var resolver = new BaseResolver(_pullRequests, _commits, new CommitAncestry(_gitCommits, gateways, clock), new DiffProvider(diffCache, gateways, clock), gateways, processing, clock);
    var builder = new ComparisonBuilder(resolver, new CommitReportReader(_reports, _codec), views, A.Fake<IComparisonRepository>(), clock);

    var notificationOptions = A.Fake<IOptionsMonitor<NotificationOptions>>();
    A.CallTo(() => notificationOptions.CurrentValue).Returns(new NotificationOptions());
    var comments = new CommentPublisher(gateways, _pullRequests, notificationOptions, urls, clock, NullLogger<CommentPublisher>.Instance);

    A.CallTo(() => _gitHub.CreateCheckRunAsync(A<ForgeRepositoryRef>._, A<GitHubCheckRun>._, A<CancellationToken>._))
     .ReturnsLazily((ForgeRepositoryRef _, GitHubCheckRun run, CancellationToken _) =>
       {
         _checkRuns.Add(run);

         return Task.FromResult<long?>(500 + _checkRuns.Count);
       });

    _service = new NotificationService(
      builder,
      views,
      _posted,
      _repositoryFlags,
      gateways,
      comments,
      new CheckRunPublisher(_gitHub),
      _settings,
      urls,
      clock,
      NullLogger<NotificationService>.Instance
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Publish_ShouldPostTheStatusesOfTheBuiltInRulesOnTheHeadSha()
  {
    var published = await PublishAsync(Complete(HeadId, Head), Config());

    published.ShouldBe(2);
    _published.Select(static p => (p.Status.Context, p.Status.State, p.Status.Description))
              .ShouldBe(
                [
                  ("coverage/project", StatusState.Failure, "50.00% (-25.00%) compared to bbbbbbb"),
                  ("coverage/patch", StatusState.Failure, "50.00% of diff hit (target 75.00%)"),
                ]
              );
    _published.ShouldAllBe(static p => p.Sha == Head);
    _stored.Count.ShouldBe(2);
    _stored.ShouldAllBe(static s => s.CommitId == HeadId && s.PostedAt == Now);
  }

  // M5.05: with the switch of the organization, check runs of the same names replace the statuses, and the default
  // patch check run annotates the added lines that are not covered.
  [Fact]
  public async Task Notify_ChecksEnabled_ShouldPublishCheckRunsInPlaceOfStatuses()
  {
    Settings("coverage", enabled: true, checks: true);

    var published = await PublishAsync(Complete(HeadId, Head), Config());

    published.ShouldBe(2);
    _published.ShouldBeEmpty();
    _checkRuns.Select(static r => (r.Name, r.State, r.Summary, r.HeadSha))
              .ShouldBe(
                [
                  ("coverage/project", StatusState.Failure, "50.00% (-25.00%) compared to bbbbbbb", Head),
                  ("coverage/patch", StatusState.Failure, "50.00% of diff hit (target 75.00%)", Head),
                ]
              );
    _checkRuns[0].Annotations.ShouldBeEmpty();
    // The patch adds lines 2 and 3; line 3 is missed.
    _checkRuns[1].Annotations.ShouldBe([new CheckRunAnnotation("src/a.cs", 3, 3, "Added line #L3 was not covered by tests")]);
    _stored.Select(static s => s.CheckRunId).ShouldBe([501, 502], ignoreOrder: true);
    A.CallTo(() => _gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<CommitStatus>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The Checks permission is not granted yet (403): the statuses are published instead, and the job does not fail.
  [Fact]
  public async Task Notify_ChecksPermissionMissing_ShouldFallBackToStatuses()
  {
    Settings("coverage", enabled: true, checks: true);
    A.CallTo(() => _gitHub.CreateCheckRunAsync(A<ForgeRepositoryRef>._, A<GitHubCheckRun>._, A<CancellationToken>._)).Returns((long?)null);

    var published = await PublishAsync(Complete(HeadId, Head), Config());

    published.ShouldBe(2);
    _published.Select(static p => p.Status.Context).ShouldBe(["coverage/project", "coverage/patch"]);
    _stored.ShouldAllBe(static s => s.CheckRunId == null);
    // Refused once: the next statuses of the notification do not ask again.
    A.CallTo(() => _gitHub.CreateCheckRunAsync(A<ForgeRepositoryRef>._, A<GitHubCheckRun>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Notify_GitHubChecksFalseInTheFile_ShouldKeepTheStatuses()
  {
    Settings("coverage", enabled: true, checks: true);

    await PublishAsync(Complete(HeadId, Head), Config(("github_checks", ConfigNode.FromBoolean(false))));

    _published.Count.ShouldBe(2);
    A.CallTo(_gitHub).MustNotHaveHappened();
  }

  [Fact]
  public async Task Notify_AnnotationsOff_ShouldPublishCheckRunsWithoutNotes()
  {
    Settings("coverage", enabled: true, checks: true);

    await PublishAsync(Complete(HeadId, Head), Config(("github_checks", ConfigNode.FromMap(("annotations", ConfigNode.FromBoolean(false))))));

    _checkRuns.Count.ShouldBe(2);
    _checkRuns.ShouldAllBe(static r => r.Annotations.Count == 0);
  }

  // Off by default: an organization that did not turn the switch on keeps its statuses.
  [Fact]
  public async Task Notify_ChecksSwitchOff_ShouldNeverCallTheChecksApi()
  {
    await PublishAsync(Complete(HeadId, Head), Config());

    _published.Count.ShouldBe(2);
    A.CallTo(_gitHub).MustNotHaveHappened();
  }

  [Fact]
  public async Task Notify_SameCheckRunAgain_ShouldSkipAndAStatusTurningIntoACheckRunShouldBePublished()
  {
    await PublishAsync(Complete(HeadId, Head), Config());
    Settings("coverage", enabled: true, checks: true);

    // Published as statuses before the switch: published again as check runs.
    (await PublishAsync(Complete(HeadId, Head), Config())).ShouldBe(2);
    _checkRuns.Clear();

    (await PublishAsync(Complete(HeadId, Head), Config())).ShouldBe(0);
    _checkRuns.ShouldBeEmpty();
  }

  // A name that switches from a status to a check run: the earlier status stays on GitHub, so it takes the new state too.
  [Fact]
  public async Task Notify_StatusTurningIntoACheckRun_ShouldAlsoUpdateTheEarlierStatus()
  {
    await PublishAsync(Complete(HeadId, Head), Config());
    _published.Clear();
    Settings("coverage", enabled: true, checks: true);

    (await PublishAsync(Complete(HeadId, Head), Config())).ShouldBe(2);

    _checkRuns.Count.ShouldBe(2);
    _published.Select(static p => p.Status.Context).ShouldBe(["coverage/project", "coverage/patch"]);
  }

  // The other way: the earlier check runs stay on GitHub, so they take the state of the statuses that replace them.
  [Fact]
  public async Task Notify_CheckRunTurningIntoAStatus_ShouldUpdateTheEarlierCheckRun()
  {
    Settings("coverage", enabled: true, checks: true);
    await PublishAsync(Complete(HeadId, Head), Config());
    Settings("coverage", enabled: true);

    (await PublishAsync(Complete(HeadId, Head), Config())).ShouldBe(2);

    _published.Count.ShouldBe(2);
    _stored.ShouldAllBe(static s => s.CheckRunId == null);
    A.CallTo(() => _gitHub.UpdateCheckRunAsync(A<ForgeRepositoryRef>._, 501, A<GitHubCheckRun>.That.Matches(static r => r.Name == "coverage/project"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _gitHub.UpdateCheckRunAsync(A<ForgeRepositoryRef>._, 502, A<GitHubCheckRun>.That.Matches(static r => r.Name == "coverage/patch"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Publish_SameStateAndDescription_ShouldSkip()
  {
    await PublishAsync(Complete(HeadId, Head), Config());
    _published.Clear();

    // Notified again, with nothing changed: nothing is published twice.
    (await PublishAsync(Complete(HeadId, Head), Config())).ShouldBe(0);

    _published.ShouldBeEmpty();
    _stored.Count.ShouldBe(2);
  }

  [Fact]
  public async Task Publish_OneStatusChanged_ShouldPostOnlyThatOne()
  {
    await PublishAsync(Complete(HeadId, Head), Config());
    _published.Clear();

    // A new upload covered the whole patch: the patch status changes, and the project one with it.
    Sessions(HeadId, 1, Report(File("src/a.cs", (1, 1), (2, 1), (3, 1), (4, 0))));

    (await PublishAsync(Complete(HeadId, Head, revision: 2), Config())).ShouldBe(2);
    _published.Select(static p => (p.Status.Context, p.Status.State))
              .ShouldBe([("coverage/project", StatusState.Success), ("coverage/patch", StatusState.Success)]);

    _published.Clear();
    // The same description with another state is still a change.
    _stored.RemoveAll(static s => s.Status.Context == "coverage/patch");
    _stored.Add(new PostedStatus(HeadId, new CommitStatus("coverage/patch", StatusState.Failure, "100.00% of diff hit (target 75.00%)"), null, Now));

    (await PublishAsync(Complete(HeadId, Head, revision: 2), Config())).ShouldBe(1);
    _published.ShouldHaveSingleItem().Status.Context.ShouldBe("coverage/patch");
  }

  [Fact]
  public async Task Publish_AllUploadsFailed_ShouldPostErrorOnExpectedContexts()
  {
    var config = Config(("codecov", ConfigNode.FromMap(("notify", ConfigNode.FromMap(("notify_error", ConfigNode.FromBoolean(true)))))));

    var published = await PublishAsync(Failed(), config);

    published.ShouldBe(2);
    _published.Select(static p => (p.Status.Context, p.Status.State, p.Status.Description))
              .ShouldBe(
                [
                  ("coverage/project", StatusState.Error, $"Coverage upload processing failed, see https://coverage.example.com/github/octo/app/commit/{Head.Value}"),
                  ("coverage/patch", StatusState.Error, $"Coverage upload processing failed, see https://coverage.example.com/github/octo/app/commit/{Head.Value}"),
                ]
              );
    // No report is read and no comparison is built for a commit without any report.
    A.CallTo(_reports).MustNotHaveHappened();
  }

  // Without notify_error, a commit whose every upload failed posts nothing, as Codecov.
  [Fact]
  public async Task Publish_AllUploadsFailedWithoutNotifyError_ShouldPostNothing()
  {
    (await PublishAsync(Failed(), Config())).ShouldBe(0);

    _published.ShouldBeEmpty();
  }

  [Fact]
  public async Task Publish_AllUploadsFailed_ShouldSkipTheRulesForPullRequestsOnAPushBuild()
  {
    var config = Config(
      ("codecov", ConfigNode.FromMap(("notify", ConfigNode.FromMap(("notify_error", ConfigNode.FromBoolean(true)))))),
      ("coverage", ConfigNode.FromMap(("status", ConfigNode.FromMap(("patch", ConfigNode.FromMap(("default", ConfigNode.FromMap(("only_pulls", ConfigNode.FromBoolean(true))))))))))
    );

    await PublishAsync(Failed(), config);

    _published.Select(static p => p.Status.Context).ShouldBe(["coverage/project"]);
  }

  [Fact]
  public async Task Publish_ShouldUseConfiguredPrefix()
  {
    Settings("codecov", enabled: true);

    await PublishAsync(Complete(HeadId, Head), Config());

    _published.Select(static p => p.Status.Context).ShouldBe(["codecov/project", "codecov/patch"]);
  }

  [Fact]
  public async Task Publish_StatusesAndCommentsDisabled_ShouldPostNothing()
  {
    Settings("coverage", enabled: false, comments: false);

    (await PublishAsync(Complete(HeadId, Head), Config())).ShouldBe(0);

    _published.ShouldBeEmpty();
    _stored.ShouldBeEmpty();
    // Nothing is even computed: the forge is not asked for a diff.
    A.CallTo(_gateway).MustNotHaveHappened();
  }

  // The acceptance of M2.23: a stuck commit gets its evaluated statuses, which say they rest on a part of the uploads.
  // No pending status is ever published.
  [Fact]
  public async Task Publish_PartialNotification_ShouldSayItOnEveryStatusAndNeverPublishAPendingOne()
  {
    (await PublishAsync(Complete(HeadId, Head), Config(), " (partial: 1/3 uploads)")).ShouldBe(2);

    _published.Count.ShouldBe(2);
    _published.ShouldAllBe(static p => p.Status.Description.EndsWith(" (partial: 1/3 uploads)", StringComparison.Ordinal));
    _published.ShouldAllBe(static p => p.Status.State == StatusState.Success || p.Status.State == StatusState.Failure || p.Status.State == StatusState.Error);
    _stored.ShouldAllBe(static s => s.Status.Description.EndsWith(" (partial: 1/3 uploads)", StringComparison.Ordinal));
  }

  // The same partial notification twice publishes nothing more: the description is stable.
  [Fact]
  public async Task Publish_PartialNotificationTwice_ShouldPublishOnce()
  {
    await PublishAsync(Complete(HeadId, Head), Config(), " (partial: 1/3 uploads)");

    (await PublishAsync(Complete(HeadId, Head), Config(), " (partial: 1/3 uploads)")).ShouldBe(0);

    _published.Count.ShouldBe(2);
  }

  // The comment needs the comparison even when the organization publishes no status.
  [Fact]
  public async Task Publish_StatusesDisabledButCommentsEnabled_ShouldStillCompareAndPostNoStatus()
  {
    Settings("coverage", enabled: false);

    (await PublishAsync(Complete(HeadId, Head), Config())).ShouldBe(0);

    _published.ShouldBeEmpty();
    _stored.ShouldBeEmpty();
    A.CallTo(() => _commits.FindByShaAsync(A<long>._, Parent, A<CancellationToken>._)).MustHaveHappened();
  }

  // A commit whose every upload failed has no comparison: the comment of its pull request stays as it is.
  [Fact]
  public async Task Publish_CommitInError_ShouldLeaveTheCommentUntouched()
  {
    await PublishAsync(Failed(), Config());

    A.CallTo(() => _gateway.ListIssueCommentsAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _gateway.CreateIssueCommentAsync(A<ForgeRepositoryRef>._, A<int>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _gateway.UpdateIssueCommentAsync(A<ForgeRepositoryRef>._, A<long>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _pullRequests.UpdateAsync(A<PullRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The acceptance of M2.19: the page of the pull request in the Web console, else the page of the commit.
  [Fact]
  public async Task Publish_PushBuild_ShouldLinkToThePageOfTheCommit()
  {
    await PublishAsync(Complete(HeadId, Head), Config());

    _published.ShouldAllBe(static p => p.Url == $"https://coverage.example.com/github/octo/app/commit/{Head.Value}");
    _stored.ShouldAllBe(static s => s.TargetUrl == $"https://coverage.example.com/github/octo/app/commit/{Head.Value}");
  }

  [Fact]
  public async Task Publish_PullRequest_ShouldLinkToThePageOfThePull()
  {
    var pull = PullRequest.Create(1, new ForgePullInfo(12, ForgePullState.Open, "A pull", "octocat", Head, "topic", "main", Parent), Now);
    A.CallTo(() => _pullRequests.FindOpenByHeadShaAsync(A<long>._, Head, A<CancellationToken>._)).Returns(pull);
    A.CallTo(() => _gateway.ListPullFilesAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullFiles([new ForgeChangedFile("src/a.cs", null, ForgeChangedFileStatus.Modified, "@@ -1,2 +1,4 @@\n a\n+x\n+y\n b")], Truncated: false));

    await PublishAsync(Complete(HeadId, Head), Config());

    _published.Count.ShouldBe(2);
    _published.ShouldAllBe(static p => p.Url == "https://coverage.example.com/github/octo/app/pull/12");
    // On the head of the pull request, never on a merge commit.
    _published.ShouldAllBe(static p => p.Sha == Head);
  }

  [Fact]
  public async Task Publish_WithoutPublicAddress_ShouldPostWithoutLink()
  {
    _urls.WebPublicUrl = null;

    await PublishAsync(Complete(HeadId, Head), Config());

    _published.Count.ShouldBe(2);
    _published.ShouldAllBe(static p => p.Url == null);
  }

  // A flag that only a rule names waits through `flag_management.default_rules`: its status is held back until
  // enough sessions carry it, while the statuses without flag are published.
  [Fact]
  public async Task Publish_RuleFlagWaitingThroughTheDefaultRules_ShouldHoldItsStatusBack()
  {
    await PublishAsync(Complete(HeadId, Head), WaitingFlagConfig(notifyError: false));

    _published.ShouldNotBeEmpty();
    _published.ShouldNotContain(static p => p.Status.Context == "coverage/project/backend");
  }

  // Every upload failed: no session will ever carry the flag, so its wait holds no error status back.
  [Fact]
  public async Task Publish_AllUploadsFailedWithARuleFlagThatWaits_ShouldStillPostItsError()
  {
    await PublishAsync(Failed(), WaitingFlagConfig(notifyError: true));

    _published.ShouldContain(static p => p.Status.Context == "coverage/project/backend" && p.Status.State == StatusState.Error);
  }

  // As the `repository.flags` of Codecov: a flag that is only declared, and never received coverage, has no status.
  [Fact]
  public async Task Publish_FlagDeclaredButNeverUploaded_ShouldHaveNoStatus()
  {
    await PublishAsync(Complete(HeadId, Head), FlagStatusConfig(individual: "e2e"));

    _published.ShouldNotBeEmpty();
    _published.ShouldNotContain(static p => p.Status.Context == "coverage/project/e2e");
  }

  // A flag the repository received on earlier commits keeps its status on a commit that does not carry it: a branch
  // protection that requires the context is not left waiting.
  [Fact]
  public async Task Publish_FlagKnownToTheRepositoryButAbsentFromTheHead_ShouldStillHaveItsStatus()
  {
    A.CallTo(() => _repositoryFlags.ListAsync(_repository.Id, A<CancellationToken>._)).Returns(["backend"]);

    await PublishAsync(Complete(HeadId, Head), FlagStatusConfig(individual: null));

    var status = _published.Single(static p => p.Status.Context == "coverage/project/backend").Status;
    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("No coverage information found on head");
  }

  // Every upload failed: the error goes to the contexts of the flags the repository knows, not of the declared ones.
  [Fact]
  public async Task Publish_AllUploadsFailed_ShouldPostErrorOnTheContextsOfTheKnownFlagsOnly()
  {
    A.CallTo(() => _repositoryFlags.ListAsync(_repository.Id, A<CancellationToken>._)).Returns(["backend"]);

    await PublishAsync(Failed(), FlagStatusConfig(individual: "e2e", notifyError: true));

    _published.ShouldContain(static p => p.Status.Context == "coverage/project/backend" && p.Status.State == StatusState.Error);
    _published.ShouldNotContain(static p => p.Status.Context == "coverage/project/e2e");
  }

  // Each rule sees the reports through its own flags: a flag status only counts the sessions of its flag.
  [Fact]
  public async Task Publish_FlagStatus_ShouldEvaluateTheSessionsOfTheFlagOnly()
  {
    _blobs[1] = Report(File("src/a.cs", (1, 1), (2, 1), (3, 0), (4, 0)));
    _blobs[3] = Report(File("web/b.ts", (1, 1), (2, 1)));
    A.CallTo(() => _reports.ListSessionsAsync(HeadId, A<CancellationToken>._))
     .Returns([Session.FromUpload(HeadId, 1, 1, ["back"], null, null, null, default), Session.FromUpload(HeadId, 2, 3, ["front"], null, null, null, default)]);
    A.CallTo(() => _reports.GetUploadCoverageAsync(3, A<CancellationToken>._)).Returns([3]);
    var config = Config(
      (
        "flag_management",
        ConfigNode.FromMap(
          (
            "individual_flags",
            ConfigNode.FromList(ConfigNode.FromMap(("name", ConfigNode.FromText("front")), ("statuses", ConfigNode.FromList(ConfigNode.FromMap(("type", ConfigNode.FromText("project")), ("target", ConfigNode.FromNumber(90)))))))
          )
        )
      )
    );

    await PublishAsync(Complete(HeadId, Head), config);

    // The base has no session of the flag: as Codecov, there is nothing to compare with, whatever the target.
    var front = _published.Single(static p => p.Status.Context == "coverage/project/front");
    front.Status.State.ShouldBe(StatusState.Success);
    front.Status.Description.ShouldBe("No coverage information found on base report");

    // With the flag on the base too, its own sessions are compared with its target.
    _blobs[2] = Report(File("web/b.ts", (1, 1), (2, 0)));
    A.CallTo(() => _reports.ListSessionsAsync(BaseId, A<CancellationToken>._)).Returns([Session.FromUpload(BaseId, 1, 2, ["front"], null, null, null, default)]);
    _published.Clear();

    await PublishAsync(Complete(HeadId, Head), config);

    front = _published.Single(static p => p.Status.Context == "coverage/project/front");
    front.Status.State.ShouldBe(StatusState.Success);
    front.Status.Description.ShouldBe("100.00% (target 90.00%)");
  }

  // A rule that asks to post nothing leaves its context alone, and the others are still published.
  [Fact]
  public async Task Publish_RuleThatPostsNothing_ShouldLeaveItsContextAlone()
  {
    var config = Config(("coverage", ConfigNode.FromMap(("status", ConfigNode.FromMap(("patch", ConfigNode.FromMap(("default", ConfigNode.FromMap(("only_pulls", ConfigNode.FromBoolean(true)))))))))));

    await PublishAsync(Complete(HeadId, Head), config);

    _published.Select(static p => p.Status.Context).ShouldBe(["coverage/project"]);
  }

  // The forge refuses one status: nothing is stored for it, so the next notification publishes it again.
  [Fact]
  public async Task Publish_ForgeRefuses_ShouldNotStoreTheStatus()
  {
    A.CallTo(() => _gateway.CreateStatusAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<CommitStatus>.That.Matches(s => s.Context == "coverage/patch"), A<string?>._, A<CancellationToken>._))
     .Throws(new BadGatewayException("GitHub.Unavailable", "GitHub does not answer."));

    await Should.ThrowAsync<BadGatewayException>(() => PublishAsync(Complete(HeadId, Head), Config()));

    _stored.Select(static s => s.Status.Context).ShouldBe(["coverage/project"]);
  }

  [Fact]
  public async Task Publish_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _service.NotifyAsync(null!, Forge.GitHub, Complete(HeadId, Head), Config(), 0, false, null, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _service.NotifyAsync(_repository, Forge.GitHub, null!, Config(), 0, false, null, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _service.NotifyAsync(_repository, Forge.GitHub, Complete(HeadId, Head), null!, 0, false, null, Ct));
  }

  // The built-in defaults, as the resolver merges them: project and patch on, adjust_base off to keep the numbers plain.
  private static CoverageConfig Config(params (string Key, ConfigNode Value)[] entries)
  {
    var status = ConfigNode.FromMap(
      ("project", ConfigNode.FromMap(("default", ConfigNode.FromMap(("removed_code_behavior", ConfigNode.FromText("off")))))),
      ("patch", ConfigNode.FromBoolean(true))
    );
    var coverage = entries.Any(static e => e.Key == "coverage")
                     ? ConfigNode.FromMap(
                       (
                         "status",
                         ConfigNode.FromMap(
                           ("project", status.Find("project")!),
                           ("patch", entries.Single(static e => e.Key == "coverage").Value.Find("status", "patch")!)
                         )
                       )
                     )
                     : ConfigNode.FromMap(("status", status));

    return CoverageConfig.Create(ConfigNode.FromMap([("coverage", coverage), .. entries.Where(static e => e.Key != "coverage")]));
  }

  // A project status for every flag through the default rules, and one more declared for a flag by name.
  private static CoverageConfig FlagStatusConfig(string? individual, bool notifyError = false)
  {
    var statuses = ConfigNode.FromList(ConfigNode.FromMap(("type", ConfigNode.FromText("project"))));
    var defaults = ConfigNode.FromMap(("statuses", statuses));
    var management = individual is null
                       ? ConfigNode.FromMap(("default_rules", defaults))
                       : ConfigNode.FromMap(
                         ("default_rules", defaults),
                         ("individual_flags", ConfigNode.FromList(ConfigNode.FromMap(("name", ConfigNode.FromText(individual)), ("statuses", statuses))))
                       );

    return CoverageConfig.Create(
      ConfigNode.FromMap(
        ("codecov", ConfigNode.FromMap(("notify", ConfigNode.FromMap(("notify_error", ConfigNode.FromBoolean(notifyError)))))),
        ("coverage", ConfigNode.FromMap(("status", ConfigNode.FromMap(("project", ConfigNode.FromMap(("default", ConfigNode.FromMap()))))))),
        ("flag_management", management)
      )
    );
  }

  private static CoverageConfig WaitingFlagConfig(bool notifyError)
  {
    var backend = ConfigNode.FromMap(("flags", ConfigNode.FromList(ConfigNode.FromText("backend"))));
    var project = ConfigNode.FromMap(("default", ConfigNode.FromMap()), ("backend", backend));
    var defaults = ConfigNode.FromMap(("after_n_builds", ConfigNode.FromNumber(2)));

    return CoverageConfig.Create(
      ConfigNode.FromMap(
        ("codecov", ConfigNode.FromMap(("notify", ConfigNode.FromMap(("notify_error", ConfigNode.FromBoolean(notifyError)))))),
        ("coverage", ConfigNode.FromMap(("status", ConfigNode.FromMap(("project", project))))),
        ("flag_management", ConfigNode.FromMap(("default_rules", defaults)))
      )
    );
  }

  private static FileCoverage File(string path, params (int Line, long Hits)[] lines) =>
    FileCoverage.Create(path, lines.Select(static line => new LineCoverage(line.Line, line.Hits, 0, 0, false, null)));

  private static CoverageReport Report(params FileCoverage[] files) => CoverageReport.Create(files);

  private static Commit Complete(long id, GitSha sha, int revision = 1)
  {
    var commit = Commit.Create(1, sha, Now.AddHours(-2));

    commit.MarkPending("main", null, Now.AddHours(-2));

    for (var i = 0; i < revision; i++)
    {
      commit.RecordFinalization(CommitState.Complete, 1, default, 1, 1, CarryforwardStatus.None, "source", Now.AddHours(-1));
    }

    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, id);

    return commit;
  }

  private static Commit Failed()
  {
    var commit = Commit.Create(1, Head, Now.AddHours(-2));

    commit.MarkPending("main", null, Now.AddHours(-2));
    commit.RecordFinalization(CommitState.Error, 0, default, 0, 0, CarryforwardStatus.None, "source", Now.AddHours(-1));
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, HeadId);

    return commit;
  }

  private Task<int> PublishAsync(Commit commit, CoverageConfig config, string? suffix = null) =>
    _service.NotifyAsync(_repository, Forge.GitHub, commit, config, 0, false, suffix, Ct);

  private void Settings(string prefix, bool enabled, bool comments = true, bool checks = false)
  {
    A.CallTo(() => _settings.GetAsync(A<CancellationToken>._)).Returns(new OrganizationSettingsSnapshot(prefix, enabled, comments, null, checks));
  }

  private void Sessions(long commitId, byte blob, CoverageReport report)
  {
    _blobs[blob] = report;
    A.CallTo(() => _reports.ListSessionsAsync(commitId, A<CancellationToken>._)).Returns([Session.FromUpload(commitId, 1, blob, [], null, null, null, report.Totals)]);
    A.CallTo(() => _reports.GetUploadCoverageAsync(blob, A<CancellationToken>._)).Returns([blob]);
  }

  #endregion
}