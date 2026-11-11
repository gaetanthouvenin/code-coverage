// ------------------------------------------------------------------------------------------------
// <copyright file="FinalizeCommitCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Commands.FinalizeCommit;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Commits;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Commits.Commands.FinalizeCommit;

public sealed class FinalizeCommitCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly ICommitConfigRepository _configs = A.Fake<ICommitConfigRepository>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly ICoverageConfigParser _configParser = A.Fake<ICoverageConfigParser>();

  private readonly IOrganizationSettingsProvider _organizationSettings = A.Fake<IOrganizationSettingsProvider>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly IRepositoryFlagRepository _repositoryFlags = A.Fake<IRepositoryFlagRepository>();

  private readonly IBranchRepository _branches = A.Fake<IBranchRepository>();

  private readonly ICoverageReportCodec _codec = A.Fake<ICoverageReportCodec>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly Dictionary<long, CoverageReport> _coverage = [];

  private readonly Commit _commit;

  private IReadOnlyList<Session> _savedSessions = [];

  private IReadOnlyList<CommitFlagTotals> _savedTotals = [];

  private FinalizationWakeUps? _savedWakeUps;

  private Branch? _savedBranch;

  private readonly NotificationOptions _notificationOptions = new();

  private CoverageReport? _encoded;

  #endregion

  #region Ctors

  public FinalizeCommitCommandHandlerTests()
  {
    _commit = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_commit, 9L);

    var repository = TrackedRepository.Create(77, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _gateways.GetGateway(A<Forge>._)).Returns(_gateway);
    // A branch without a row exists on the forge, at the commit finalized, unless a test says otherwise.
    A.CallTo(() => _gateway.GetBranchHeadAsync(A<ForgeRepositoryRef>._, A<string>._, A<CancellationToken>._)).Returns(Sha);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(_commit);
    A.CallTo(() => _repositories.FindAsync(5, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now));
    Config(null);
    A.CallTo(() => _organizationSettings.GetAsync(A<CancellationToken>._)).Returns(new OrganizationSettingsSnapshot("coverage", true, true, null));
    A.CallTo(() => _reports.GetUploadCoverageAsync(A<long>._, A<CancellationToken>._))
     .ReturnsLazily((long id, CancellationToken _) => Task.FromResult<byte[]?>([(byte)id]));
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._)).ReturnsLazily((ReadOnlyMemory<byte> blob) => _coverage[blob.Span[0]]);
    A.CallTo(() => _codec.Encode(A<CoverageReport>._)).Invokes((CoverageReport report) => _encoded = report).Returns([1]);
    A.CallTo(() => _codec.EncodeFileIndex(A<CoverageReport>._)).Returns([2]);
    A.CallTo(() => _reports.SaveFinalizedAsync(A<Commit>._, A<byte[]>._, A<byte[]>._, A<IReadOnlyList<Session>>._, A<IReadOnlyList<CommitFlagTotals>>._, A<Branch?>._, A<FinalizationWakeUps>._, A<CancellationToken>._))
     .Invokes((Commit _, byte[] _, byte[] _, IReadOnlyList<Session> sessions, IReadOnlyList<CommitFlagTotals> totals, Branch? branch, FinalizationWakeUps wakeUps, CancellationToken _) =>
       {
         _savedSessions = sessions;
         _savedTotals = totals;
         _savedBranch = branch;
         _savedWakeUps = wakeUps;
       });
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_WhenUploadStillProcessing_ShouldStop()
  {
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)), Upload.Receive(9, 2, "k2", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    A.CallTo(_reports).MustNotHaveHappened();
    _commit.ReportRevision.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_ShouldNumberSessionsBySequence()
  {
    Uploads(Processed(3, ["a"], Report("src/A.cs", 1)), Processed(1, ["b"], Report("src/B.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedSessions.Select(static s => s.Number).ShouldBe([1, 3]);
    _commit.SessionCount.ShouldBe(2);
    _commit.UploadedSessionCount.ShouldBe(2);
    _commit.ReportRevision.ShouldBe(1);
    _commit.State.ShouldBe(CommitState.Complete);
  }

  // The flags of the sessions become flags of the repository, which decide the flag statuses of its commits. They are
  // recorded before the report is saved: the notification that the save queues reads them.
  [Fact]
  public async Task Handle_ShouldRecordTheFlagsOfTheSessionsForTheRepositoryBeforeTheSave()
  {
    Uploads(Processed(1, ["unit", "Back"], Report("src/A.cs", 1)), Processed(2, ["unit"], Report("src/B.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    A.CallTo(
       () => _repositoryFlags.AddMissingAsync(
         _commit.RepositoryId,
         A<IReadOnlyCollection<string>>.That.Matches(flags => flags.Order(StringComparer.Ordinal).SequenceEqual(new[] { "Back", "unit" })),
         Now,
         A<CancellationToken>._
       )
     )
     .MustHaveHappenedOnceExactly()
     .Then(
       A.CallTo(_reports)
        .Where(call => call.Method.Name == nameof(ICommitReportRepository.SaveFinalizedAsync))
        .MustHaveHappenedOnceExactly()
     );
  }

  // Nothing was finalized: nothing is recorded.
  [Fact]
  public async Task Handle_WhenUploadStillProcessing_ShouldRecordNoFlag()
  {
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)), Upload.Receive(9, 2, "k2", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    A.CallTo(_repositoryFlags).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_CommitWithoutBranch_ShouldSaveNoBranch()
  {
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedBranch.ShouldBeNull();
    A.CallTo(_branches).MustNotHaveHappened();
  }

  // No push told about the branch yet (the App was installed later, or the upload came first): the finalization
  // creates its row, so that a badge of the branch has something to read.
  [Fact]
  public async Task Handle_BranchWithoutRow_ShouldCreateItCoveredByTheCommit()
  {
    _commit.MarkPending("feature/x", null, Now);
    A.CallTo(() => _branches.FindAsync(5, "feature/x", A<CancellationToken>._)).Returns((Branch?)null);
    A.CallTo(() => _gateway.GetBranchHeadAsync(A<ForgeRepositoryRef>._, "feature/x", A<CancellationToken>._)).Returns(Sha);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    var branch = _savedBranch.ShouldNotBeNull();
    branch.RepositoryId.ShouldBe(5);
    branch.Name.ShouldBe("feature/x");
    branch.HeadSha.ShouldBe(Sha);
    branch.CoveredCommitId.ShouldBe(9);
    branch.CoveredAt.ShouldBe(Now);
    // Saved by the transaction of the finalization only, never on its own.
    A.CallTo(() => _branches.AddAsync(A<Branch>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_HeadOfItsBranch_ShouldBecomeTheCoveredHead()
  {
    _commit.MarkPending("main", null, Now);
    var branch = Branch.Create(5, "main", Sha, Now.AddHours(-1));
    A.CallTo(() => _branches.FindAsync(5, "main", A<CancellationToken>._)).Returns(branch);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedBranch.ShouldBeSameAs(branch);
    branch.CoveredCommitId.ShouldBe(9);
    A.CallTo(() => _branches.UpdateAsync(A<Branch>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // F16. The commit of a pull request names the branch of its head, a fork's branch for instance: the repository has no
  // such branch, and a row would show the contributor's commit as the head of a branch, the default one included.
  [Fact]
  public async Task Handle_CommitOfAPullRequestWhoseBranchHasNoRow_ShouldCreateNoBranch()
  {
    _commit.MarkPending("main", 12, Now);
    A.CallTo(() => _branches.FindAsync(5, "main", A<CancellationToken>._)).Returns((Branch?)null);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedBranch.ShouldBeNull();
    A.CallTo(() => _gateway.GetBranchHeadAsync(A<ForgeRepositoryRef>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // F16. A late finalization does not bring back a branch that was deleted.
  [Fact]
  public async Task Handle_BranchDeletedOnTheForge_ShouldCreateNoBranch()
  {
    _commit.MarkPending("feature/gone", null, Now);
    A.CallTo(() => _branches.FindAsync(5, "feature/gone", A<CancellationToken>._)).Returns((Branch?)null);
    A.CallTo(() => _gateway.GetBranchHeadAsync(A<ForgeRepositoryRef>._, "feature/gone", A<CancellationToken>._)).Returns((GitSha?)null);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedBranch.ShouldBeNull();
  }

  // F16. The row starts where the branch is on the forge, not at the commit that was finalized.
  [Fact]
  public async Task Handle_BranchWithoutRowThatMovedOn_ShouldCreateItAtItsHead()
  {
    var head = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");
    _commit.MarkPending("feature/x", null, Now);
    A.CallTo(() => _branches.FindAsync(5, "feature/x", A<CancellationToken>._)).Returns((Branch?)null);
    A.CallTo(() => _gateway.GetBranchHeadAsync(A<ForgeRepositoryRef>._, "feature/x", A<CancellationToken>._)).Returns(head);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedBranch.ShouldNotBeNull().HeadSha.ShouldBe(head);
  }

  // The head of the branch moved on, and nothing links it to this commit within the ancestry that is read.
  [Fact]
  public async Task Handle_CommitOutOfTheAncestryOfTheHead_ShouldLeaveTheCoveredHeadToTheAncestry()
  {
    _commit.MarkPending("main", null, Now);
    var branch = Branch.Create(5, "main", GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3"), Now.AddHours(-1));
    A.CallTo(() => _branches.FindAsync(5, "main", A<CancellationToken>._)).Returns(branch);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedBranch.ShouldBeSameAs(branch);
    branch.CoveredCommitId.ShouldBeNull();
    branch.Version.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_AllUploadsFailedOnTheHeadOfItsBranch_ShouldNotBecomeTheCoveredHead()
  {
    _commit.MarkPending("main", null, Now);
    var branch = Branch.Create(5, "main", Sha, Now.AddHours(-1));
    branch.Cover(9, Now.AddHours(-1));
    A.CallTo(() => _branches.FindAsync(5, "main", A<CancellationToken>._)).Returns(branch);
    var failed = Upload.Receive(9, 1, "k", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);
    failed.MarkFailed("Upload.NoFileMapped", "none", 1, 0, 1, ProcessingLog.Empty, Now);
    Uploads(failed);

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _commit.State.ShouldBe(CommitState.Error);
    branch.CoveredCommitId.ShouldBeNull();
  }

  // The ancestry may save what it fetches: the finalization must not be on the commit yet, or that save would write it
  // outside the transaction of SaveFinalizedAsync.
  [Fact]
  public async Task Handle_ShouldReadTheCoveredHeadBeforeRecordingTheFinalization()
  {
    _commit.MarkPending("main", null, Now);
    var revisionWhenRead = -1;
    A.CallTo(() => _commits.ListByShasAsync(5, A<IReadOnlyCollection<GitSha>>._, A<CancellationToken>._))
     .Invokes(() => revisionWhenRead = _commit.ReportRevision)
     .Returns([]);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    revisionWhenRead.ShouldBe(0);
    _commit.ReportRevision.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_AllUploadsFailed_ShouldSetError()
  {
    var failed = Upload.Receive(9, 1, "k", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);
    failed.MarkFailed("Upload.NoFileMapped", "none", 1, 0, 1, ProcessingLog.Empty, Now);
    Uploads(failed);

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _commit.State.ShouldBe(CommitState.Error);
    _savedSessions.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_OneUploadFailed_ShouldStayComplete()
  {
    var failed = Upload.Receive(9, 2, "k2", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);
    failed.MarkFailed("Upload.NoFileMapped", "none", 1, 0, 1, ProcessingLog.Empty, Now);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)), failed);

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _commit.State.ShouldBe(CommitState.Complete);
    _commit.UploadedSessionCount.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_ShouldWriteProjectAndFlagTotals()
  {
    Config("flags: declared");
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1, 0)), Processed(2, ["a", "b"], Report("lib/B.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedTotals.Select(static t => t.Flag).ShouldBe([CommitFlagTotals.ProjectFlag, "a", "b", "declared"]);
    _savedTotals[0].Lines.ShouldBe(3);
    _savedTotals[0].Hits.ShouldBe(2);
    _savedTotals.Single(static t => t.Flag == "a").Lines.ShouldBe(3);
    _savedTotals.Single(static t => t.Flag == "b").Lines.ShouldBe(1);
    _savedTotals.Single(static t => t.Flag == "declared").Lines.ShouldBe(0);
    _savedTotals.Single(static t => t.Flag == "b").IsCarriedForward.ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_JoinedFalseFlag_ShouldStayOutOfTheProjectAndInItsFlag()
  {
    Config("flags: solo-not-joined");
    Uploads(Processed(1, ["solo"], Report("src/A.cs", 1)), Processed(2, ["main"], Report("src/B.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedTotals.Single(static t => t.Flag == CommitFlagTotals.ProjectFlag).Lines.ShouldBe(1);
    _savedTotals.Single(static t => t.Flag == "solo").Lines.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_ShuffledArrival_ShouldProduceSameReport()
  {
    var first = Processed(1, ["a"], Report("src/A.cs", 1, 0));
    var second = Processed(2, ["b"], Report("src/A.cs", 0, 1));

    Uploads(first, second);
    await Handler().Handle(new FinalizeCommitCommand(9), Ct);
    var forward = _encoded!;

    Uploads(second, first);
    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _encoded.ShouldNotBeSameAs(forward);
    _encoded!.Totals.ShouldBe(forward.Totals);
    _encoded.Files[0].Lines.ShouldBe(forward.Files[0].Lines);
    _savedSessions.Select(static s => s.Number).ShouldBe([1, 2]);
  }

  [Fact]
  public async Task Handle_UnknownCommit_ShouldThrowNotFound()
  {
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns((Commit?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(new FinalizeCommitCommand(9), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.NotFound);
  }

  [Fact]
  public async Task Handle_CoverageGone_ShouldThrowUnprocessable()
  {
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));
    A.CallTo(() => _reports.GetUploadCoverageAsync(A<long>._, A<CancellationToken>._)).Returns((byte[]?)null);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await Handler().Handle(new FinalizeCommitCommand(9), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.Compacted);
  }

  // The notification of a finalized commit is queued by the save itself, in its transaction, and debounced.
  [Fact]
  public async Task Handle_CompleteCommit_ShouldQueueTheDebouncedNotificationInTheSave()
  {
    _commit.MarkPending(null, null, Now);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    var job = _savedWakeUps!.Notification.ShouldNotBeNull();
    job.Type.ShouldBe(JobTypes.NotifyCommit);
    job.PayloadJson.ShouldBe("""{"CommitId":9,"Force":false}""");
    job.LockKey.ShouldBe("commit:9");
    job.DedupKey.ShouldBe("notify:9");
    job.RunAfter.ShouldBe(Now.AddSeconds(20));
    job.MaxDelay.ShouldBe(TimeSpan.FromSeconds(300));
    job.Coalesce.ShouldBeFalse();
    // The organization of the scope: the job names none.
    job.OrganizationId.ShouldBeNull();
    job.IsInstance.ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_CompleteCommit_ShouldDebounceWithTheSettings()
  {
    _notificationOptions.DebounceSeconds = 0;
    _notificationOptions.MaxDelaySeconds = 45;
    _commit.MarkPending(null, null, Now);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    var job = _savedWakeUps!.Notification.ShouldNotBeNull();
    job.RunAfter.ShouldBe(Now);
    job.MaxDelay.ShouldBe(TimeSpan.FromSeconds(45));
  }

  // Every upload failed: the commit is in error, and its notification says so on each expected status.
  [Fact]
  public async Task Handle_EveryUploadFailed_ShouldStillQueueTheNotification()
  {
    _commit.MarkPending(null, null, Now);
    var failed = Upload.Receive(9, 1, "k1", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);
    failed.MarkFailed("Upload.ParseFailed", "unreadable", 1, 0, 0, ProcessingLog.Empty, Now);
    Uploads(failed);

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _commit.State.ShouldBe(CommitState.Error);
    _savedWakeUps!.Notification.ShouldNotBeNull();
  }

  [Fact]
  public async Task Handle_ManualTrigger_ShouldNotQueueTheNotification()
  {
    Notify(("manual_trigger", ConfigNode.FromBoolean(true)));
    _commit.MarkPending(null, null, Now);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedWakeUps!.Notification.ShouldBeNull();
    // The commit is finalized all the same: only its notification waits.
    _commit.State.ShouldBe(CommitState.Complete);
  }

  [Theory]
  [InlineData(1, false)]
  [InlineData(2, true)]
  [InlineData(3, true)]
  public async Task Handle_AfterNBuilds_ShouldQueueTheNotificationOnceReached(int uploads, bool queued)
  {
    Notify(("after_n_builds", ConfigNode.FromNumber(2)));
    _commit.MarkPending(null, null, Now);
    Uploads([.. Enumerable.Range(1, uploads).Select(i => Processed(i, ["a"], Report("src/A.cs", 1)))]);

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    (_savedWakeUps!.Notification is not null).ShouldBe(queued);
  }

  private void Notify(params (string Key, ConfigNode Value)[] entries)
  {
    const string Yaml = "codecov: notify";

    A.CallTo(() => _configs.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._))
     .Returns(CommitConfig.Create(5, Sha, ConfigSource.Commit, null, Yaml, [], [], Now));
    A.CallTo(() => _configParser.Parse(Yaml))
     .Returns(new ConfigParseResult(ConfigNode.FromMap(("codecov", ConfigNode.FromMap(("notify", ConfigNode.FromMap(entries))))), null, []));
  }

  private static CoverageReport Report(string path, params int[] hits) =>
    CoverageReport.Create([FileCoverage.Create(path, hits.Select(static (hit, index) => new LineCoverage(index + 1, hit, 0, 0, false, null)))]);

  private Upload Processed(int sequence, string[] flags, CoverageReport report)
  {
    var upload = Upload.Receive(9, sequence, $"key-{sequence}", flags, null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);
    var id = sequence + 100;

    typeof(Upload).GetProperty(nameof(Upload.Id))!.SetValue(upload, (long)id);
    upload.MarkProcessed(1, 1, 0, report.Totals, ProcessingLog.Empty, Now);
    _coverage[(byte)id] = report;

    return upload;
  }

  private void Uploads(params Upload[] uploads) => A.CallTo(() => _uploads.ListByCommitAsync(9, A<CancellationToken>._)).Returns(uploads);

  private void Config(string? yaml)
  {
    A.CallTo(() => _configs.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._))
     .Returns(CommitConfig.Create(5, Sha, yaml is null ? ConfigSource.Builtin : ConfigSource.Commit, null, yaml, [], [], Now));

    if (yaml is null)
    {
      return;
    }

    var flag = yaml.Split(": ")[1];
    var isJoined = flag != "solo-not-joined";
    var name = isJoined ? flag : "solo";
    ConfigNode rule = isJoined ? ConfigNode.FromMap() : ConfigNode.FromMap(("joined", ConfigNode.FromBoolean(false)));

    A.CallTo(() => _configParser.Parse(yaml)).Returns(new ConfigParseResult(ConfigNode.FromMap(("flags", ConfigNode.FromMap((name, rule)))), null, []));
  }

  // The waiting commits are woken by the save itself, in its transaction.
  [Fact]
  public async Task Handle_CompleteCommit_ShouldWakeTheWaitingCommitsInTheSave()
  {
    _commit.MarkPending(null, null, Now);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedWakeUps!.Completed.ShouldBeTrue();
    _savedWakeUps.PendingAncestorIds.ShouldBeEmpty();
  }

  // (a) The wake-ups are saved with the finalization or not at all: a save that fails leaves the commit pending, and
  // the job that runs again still wakes the waiting commits.
  [Fact]
  public async Task Handle_SaveFailsThenRunsAgain_ShouldStillWakeTheWaitingCommits()
  {
    var retried = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(retried, 9L);
    retried.MarkPending(null, null, Now);
    _commit.MarkPending(null, null, Now);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(_commit).Once().Then.Returns(retried);
    A.CallTo(() => _reports.SaveFinalizedAsync(A<Commit>._, A<byte[]>._, A<byte[]>._, A<IReadOnlyList<Session>>._, A<IReadOnlyList<CommitFlagTotals>>._, A<Branch?>._, A<FinalizationWakeUps>._, A<CancellationToken>._))
     .Throws(new TimeoutException("lost"))
     .Once();
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Should.ThrowAsync<TimeoutException>(async () => await Handler().Handle(new FinalizeCommitCommand(9), Ct));
    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedWakeUps!.Completed.ShouldBeTrue();
  }

  // (b) A commit in error that completes is a completion: its waiting commits are woken.
  [Fact]
  public async Task Handle_CommitInErrorThatCompletes_ShouldWakeTheWaitingCommits()
  {
    _commit.RecordFinalization(CommitState.Error, 0, default, 0, 0, CarryforwardStatus.None, "source", Now);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedWakeUps!.Completed.ShouldBeTrue();
  }

  // A commit that was already complete (woken because it waits for an ancestor) does not wake the others again:
  // two waiting commits would otherwise requeue each other without end.
  [Fact]
  public async Task Handle_ReFinalizationOfACompleteCommit_ShouldWakeNoWaitingCommit()
  {
    _commit.RecordFinalization(CommitState.Complete, 1, default, 1, 1, CarryforwardStatus.AncestorPending, "source", Now);
    Uploads(Processed(1, ["a"], Report("src/A.cs", 1)));

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _commit.ReportRevision.ShouldBe(2);
    _savedWakeUps!.Completed.ShouldBeFalse();
    _savedWakeUps.PendingAncestorIds.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_AllUploadsFailed_ShouldWakeNoWaitingCommit()
  {
    var failed = Upload.Receive(9, 1, "k", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);
    failed.MarkFailed("Upload.NoFileMapped", "none", 1, 0, 1, ProcessingLog.Empty, Now);
    _commit.MarkPending(null, null, Now);
    Uploads(failed);

    await Handler().Handle(new FinalizeCommitCommand(9), Ct);

    _savedWakeUps!.Completed.ShouldBeFalse();
  }

  private FinalizeCommitCommandHandler Handler()
  {
    var resolver = new ConfigResolver(_configs, _gateways, _configParser, _organizationSettings, _clock);
    var options = A.Fake<IOptionsMonitor<ProcessingOptions>>();
    A.CallTo(() => options.CurrentValue).Returns(new ProcessingOptions());
    var trees = new RepositoryTreeProvider(A.Fake<IRepositoryTreeRepository>(), _gateways, _clock);
    var diffs = new DiffProvider(A.Fake<IDiffCacheRepository>(), _gateways, _clock);
    var ancestry = new CommitAncestry(A.Fake<IGitCommitRepository>(), _gateways, _clock);
    var carryforward = new CarryforwardResolver(ancestry, _commits, _reports, diffs, trees, _codec, options, _clock, NullLogger<CarryforwardResolver>.Instance);

    var notifications = A.Fake<IOptionsMonitor<NotificationOptions>>();
    A.CallTo(() => notifications.CurrentValue).Returns(_notificationOptions);

    return new FinalizeCommitCommandHandler(
      _commits,
      _uploads,
      _repositories,
      _directory,
      resolver,
      _reports,
      _codec,
      new CommitMerger(),
      carryforward,
      _repositoryFlags,
      _branches,
      new BranchHeadTracker(_commits, ancestry),
      _gateways,
      notifications,
      _clock
    );
  }

  #endregion
}
