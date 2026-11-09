// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessUploadCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Application.Features.Uploads.Commands.ProcessUpload;
using CodeCoverage.Application.Services;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Uploads.Commands.ProcessUpload;

public sealed class ProcessUploadCommandHandlerTests
{
  #region Fields

  private const string Worker = "worker-1";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly ICommitConfigRepository _configs = A.Fake<ICommitConfigRepository>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly ICoverageConfigParser _configParser = A.Fake<ICoverageConfigParser>();

  private readonly IOrganizationSettingsProvider _organizationSettings = A.Fake<IOrganizationSettingsProvider>();

  private readonly IRepositoryTreeRepository _trees = A.Fake<IRepositoryTreeRepository>();

  private readonly IRawUploadStore _raw = A.Fake<IRawUploadStore>();

  private readonly IReportParserCatalog _catalog = A.Fake<IReportParserCatalog>();

  private readonly IReportParser _parser = A.Fake<IReportParser>();

  private readonly IGitCommitRepository _gitCommits = A.Fake<IGitCommitRepository>();

  private readonly ICoverageReportCodec _codec = A.Fake<ICoverageReportCodec>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly Upload _upload;

  private readonly List<UploadPart> _parts = [];

  #endregion

  #region Ctors

  public ProcessUploadCommandHandlerTests()
  {
    _upload = Upload.Receive(9, 1, "key", ["back"], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);

    var commit = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 9L);

    var repository = TrackedRepository.Create(77, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _uploads.TryStartProcessingAsync(_upload.PublicId, Worker, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _uploads.FindByPublicIdAsync(_upload.PublicId, A<CancellationToken>._)).Returns(_upload);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(commit);
    A.CallTo(() => _repositories.FindAsync(5, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now));
    A.CallTo(() => _gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => _configs.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns(CommitConfig.Create(5, Sha, ConfigSource.Builtin, null, null, [], [], Now));
    A.CallTo(() => _organizationSettings.GetAsync(A<CancellationToken>._)).Returns(new OrganizationSettingsSnapshot("coverage", true, true, null));
    A.CallTo(() => _trees.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns((ForgeTree?)null);
    A.CallTo(() => _gateway.GetTreeAsync(A<ForgeRepositoryRef>._, A<GitSha>._, true, A<CancellationToken>._)).Returns((ForgeTree?)null);
    A.CallTo(() => _raw.OpenReadAsync(_upload.PublicId, A<CancellationToken>._)).ReturnsLazily(() => Parts());
    A.CallTo(() => _catalog.Detect(A<ReadOnlyMemory<byte>>._, A<string>._)).Returns(ReportFormat.Cobertura);
    A.CallTo(() => _catalog.Find(ReportFormat.Cobertura)).Returns(_parser);
    A.CallTo(() => _codec.Encode(A<CoverageReport>._)).Returns([1, 2, 3]);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_Valid_ShouldStoreTheCoverageAndQueueTheFinalization()
  {
    Report("report.xml");
    Parses(File("src/A.cs"));
    Tree("src/A.cs");

    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Processed);
    _upload.ReportFileCount.ShouldBe(1);
    _upload.ResolvedFileCount.ShouldBe(1);
    _upload.Lines.ShouldBe(2);
    _upload.Hits.ShouldBe(1);
    _upload.LockedBy.ShouldBeNull();
    A.CallTo(() => _reports.SaveProcessedUploadAsync(
               _upload,
               A<byte[]>.That.IsSameSequenceAs(new byte[] { 1, 2, 3 }),
               A<JobRequest>.That.Matches(static j => j.Type == JobTypes.FinalizeCommit && j.LockKey == "commit:9" && j.DedupKey == "finalize:9" && j.Coalesce),
               A<CancellationToken>._
             ))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_MergeCommit_ShouldMoveToTheHeadAndLogIt()
  {
    var head = GitSha.Parse(new string('b', GitSha.Length));
    var target = Commit.Create(5, head, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(target, 11L);
    var merge = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(merge, 9L);
    merge.MarkPending("feature", 12, Now);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(merge);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "title", null, head, "feature", "main", Sha, Sha));
    A.CallTo(() => _uploads.MoveToCommitAsync(_upload, head, A<DateTimeOffset>._, A<CancellationToken>._)).Returns(target);
    Report("report.xml");
    Parses(File("src/A.cs"));
    A.CallTo(() => _gateway.GetTreeAsync(A<ForgeRepositoryRef>._, head, true, A<CancellationToken>._))
     .Returns(new ForgeTree(head, [new ForgeTreeEntry("src/A.cs", head, false)], false));

    await Handler().Handle(Command(), Ct);

    _upload.ProcessingLog.Messages.ShouldHaveSingleItem().ShouldStartWith(UploadErrors.MergeCommitRewritten);
    _upload.State.ShouldBe(UploadState.Processed);
    A.CallTo(() => _reports.SaveProcessedUploadAsync(_upload, A<byte[]>._, A<JobRequest>.That.Matches(static j => j.DedupKey == "finalize:11"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // The move is saved at once: a job that fails after it and runs again finds the upload on the head, and must still
  // report the rewrite.
  [Fact]
  public async Task Handle_RetryAfterTheMove_ShouldKeepTheRewriteInTheLog()
  {
    var head = GitSha.Parse(new string('b', GitSha.Length));
    var merge = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(merge, 9L);
    merge.MarkPending("feature", 12, Now);
    var target = Commit.Create(5, head, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(target, 11L);
    target.MarkPending("feature", 12, Now);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(merge);
    A.CallTo(() => _commits.FindAsync(11, A<CancellationToken>._)).Returns(target);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "title", null, head, "feature", "main", Sha, Sha));
    A.CallTo(() => _uploads.MoveToCommitAsync(_upload, head, A<DateTimeOffset>._, A<CancellationToken>._))
     .Invokes(() => _upload.MoveToCommit(11, 1))
     .Returns(target);
    Report("report.xml");
    Parses(File("src/A.cs"));
    A.CallTo(() => _gateway.GetTreeAsync(A<ForgeRepositoryRef>._, head, true, A<CancellationToken>._))
     .Returns(new ForgeTree(head, [new ForgeTreeEntry("src/A.cs", head, false)], false));
    A.CallTo(() => _gateway.GetTreeAsync(A<ForgeRepositoryRef>._, head, true, A<CancellationToken>._))
     .Throws(new BadGatewayException("GitHub.Unavailable", "down"))
     .Once();

    await Should.ThrowAsync<BadGatewayException>(async () => await Handler().Handle(Command(), Ct));
    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Processed);
    _upload.ProcessingLog.Messages.ShouldHaveSingleItem().ShouldStartWith(UploadErrors.MergeCommitRewritten);
    A.CallTo(() => _uploads.MoveToCommitAsync(A<Upload>._, A<GitSha>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // A replay of an upload already moved to the head: the head holds its idempotency key, so the copy ends for good.
  [Fact]
  public async Task Handle_HeadAlreadyHoldsTheSameUpload_ShouldFailAsDuplicate()
  {
    var head = GitSha.Parse(new string('b', GitSha.Length));
    var merge = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(merge, 9L);
    merge.MarkPending("feature", 12, Now);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(merge);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "title", null, head, "feature", "main", Sha, Sha));
    A.CallTo(() => _uploads.MoveToCommitAsync(_upload, head, A<DateTimeOffset>._, A<CancellationToken>._))
     .ThrowsAsync(new ConflictException("Upload.Duplicate", "The head already holds this idempotency key."));
    Report("report.xml");

    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Failed);
    _upload.ErrorCode.ShouldBe("Upload.Duplicate");
    A.CallTo(_raw).MustNotHaveHappened();
    A.CallTo(() => _reports.SaveProcessedUploadAsync(_upload, null, A<JobRequest>.That.Matches(static j => j.DedupKey == "finalize:9"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_PullRequestHead_ShouldStayOnItsCommit()
  {
    var head = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(head, 9L);
    head.MarkPending("feature", 12, Now);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(head);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullInfo(12, ForgePullState.Open, "title", null, Sha, "feature", "main", Sha, null));
    Report("report.xml");
    Parses(File("src/A.cs"));
    Tree("src/A.cs");

    await Handler().Handle(Command(), Ct);

    A.CallTo(() => _uploads.MoveToCommitAsync(A<Upload>._, A<GitSha>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_AlreadyFinished_ShouldDoNothing()
  {
    A.CallTo(() => _uploads.TryStartProcessingAsync(_upload.PublicId, Worker, A<CancellationToken>._)).Returns(false);

    await Handler().Handle(Command(), Ct);

    A.CallTo(_raw).MustNotHaveHappened();
    A.CallTo(_reports).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownFormat_ShouldLogAndContinue()
  {
    Report("coverage-final.json");
    Report("report.xml");
    A.CallTo(() => _catalog.Detect(A<ReadOnlyMemory<byte>>._, "coverage-final.json")).Returns(null);
    Parses(File("src/A.cs"));
    Tree("src/A.cs");

    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Processed);
    _upload.ReportFileCount.ShouldBe(2);
    _upload.ProcessingLog.Messages.ShouldContain($"{UploadErrors.UnsupportedFormat}: coverage-final.json");
  }

  [Fact]
  public async Task Handle_NoFileResolved_ShouldFailAndStillEnqueueFinalize()
  {
    Report("report.xml");
    Parses(File("elsewhere/X.cs"));
    Tree("src/A.cs");

    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Failed);
    _upload.ErrorCode.ShouldBe(UploadErrors.NoFileMapped);
    _upload.ProcessingLog.Paths.ShouldBe(["elsewhere/X.cs"]);
    A.CallTo(() => _reports.SaveProcessedUploadAsync(_upload, null, A<JobRequest>.That.Matches(static j => j.Type == JobTypes.FinalizeCommit), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_ParseException_ShouldFailWithCode()
  {
    Report("report.xml");
    A.CallTo(() => _parser.ParseAsync(A<Stream>._, A<CancellationToken>._)).ThrowsAsync(new InvalidDataException("broken"));

    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Failed);
    _upload.ErrorCode.ShouldBe(UploadErrors.ParseFailed);
    _upload.Error!.ShouldContain("report.xml");
    A.CallTo(() => _reports.SaveProcessedUploadAsync(_upload, null, A<JobRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // A failure of the store while a report is read is not a parsing failure: the job runs again.
  [Fact]
  public async Task Handle_StoreFailsWhileReading_ShouldLetTheJobRunAgain()
  {
    Report("report.xml");
    A.CallTo(() => _parser.ParseAsync(A<Stream>._, A<CancellationToken>._)).ThrowsAsync(new TestDbException());

    await Should.ThrowAsync<TestDbException>(async () => await Handler().Handle(Command(), Ct));

    _upload.State.ShouldNotBe(UploadState.Failed);
    A.CallTo(() => _reports.SaveProcessedUploadAsync(A<Upload>._, A<byte[]?>._, A<JobRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The default execution strategy of both providers wraps a transient failure in an InvalidOperationException.
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Handle_StoreFailureWrappedByTheExecutionStrategy_ShouldLetTheJobRunAgain(bool timeout)
  {
    Report("report.xml");
    Exception cause = timeout ? new TimeoutException("expired") : new TestDbException();
    var wrapped = new InvalidOperationException("An exception has been raised that is likely due to a transient failure.", new InvalidOperationException("inner", cause));
    A.CallTo(() => _parser.ParseAsync(A<Stream>._, A<CancellationToken>._)).ThrowsAsync(wrapped);

    var thrown = await Should.ThrowAsync<InvalidOperationException>(async () => await Handler().Handle(Command(), Ct));

    thrown.ShouldBeSameAs(wrapped);
    _upload.State.ShouldNotBe(UploadState.Failed);
    A.CallTo(() => _reports.SaveProcessedUploadAsync(A<Upload>._, A<byte[]?>._, A<JobRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // An empty list (git failed on the agent) is no list: the tree of the forge is read instead.
  [Fact]
  public async Task Handle_EmptyNetworkPart_ShouldReadTheTree()
  {
    Report("report.xml");
    Network(string.Empty);
    Parses(File("A.cs"));
    Tree("src/A.cs");

    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Processed);
    A.CallTo(() => _gateway.GetTreeAsync(A<ForgeRepositoryRef>._, Sha, true, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // PLAN M1.05: the fixes left out join the processing log.
  [Fact]
  public async Task Handle_RefusedFix_ShouldBeLogged()
  {
    A.CallTo(() => _configs.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns(CommitConfig.Create(5, Sha, ConfigSource.Commit, "codecov.yml", "yaml", [], [], Now));
    A.CallTo(() => _configParser.Parse("yaml"))
     .Returns(new ConfigParseResult(ConfigNode.FromMap(("fixes", ConfigNode.FromList(ConfigNode.FromText("before(?=x)::after/")))), null, []));
    Report("report.xml");
    Parses(File("src/A.cs"));
    Tree("src/A.cs");

    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Processed);
    _upload.ProcessingLog.Messages.ShouldHaveSingleItem().ShouldContain("before(?=x)::after/");
  }

  [Fact]
  public async Task Handle_WithoutNetwork_ShouldUseTreeAndWarnIfTruncated()
  {
    Report("report.xml");
    Parses(File("src/A.cs"));
    Tree(truncated: true, "src/A.cs");

    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Processed);
    _upload.ProcessingLog.Messages.ShouldHaveSingleItem().ShouldContain("truncated");
  }

  [Fact]
  public async Task Handle_NetworkPart_ShouldNotReadTheTree()
  {
    Report("report.xml");
    Network("src/A.cs\n\nsrc/B.cs\n");
    Parses(File("src/B.cs"));

    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Processed);
    A.CallTo(() => _gateway.GetTreeAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_NoListAtAll_ShouldKeepThePathsAsUnverified()
  {
    Report("report.xml");
    Parses(File("src/A.cs"));

    await Handler().Handle(Command(), Ct);

    _upload.State.ShouldBe(UploadState.Processed);
    _upload.ProcessingLog.Messages.ShouldHaveSingleItem().ShouldContain("not verified");
  }

  [Fact]
  public async Task Handle_IgnoredPath_ShouldBeDroppedAfterTheMerge()
  {
    A.CallTo(() => _configs.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns(CommitConfig.Create(5, Sha, ConfigSource.Commit, "codecov.yml", "yaml", [], [], Now));
    A.CallTo(() => _configParser.Parse("yaml"))
     .Returns(new ConfigParseResult(ConfigNode.FromMap(("ignore", ConfigNode.FromList(ConfigNode.FromText("src/skip")))), null, []));
    Report("report.xml");
    Parses(File("src/A.cs"), File("src/skip/B.cs"));
    Tree("src/A.cs", "src/skip/B.cs");

    await Handler().Handle(Command(), Ct);

    _upload.Lines.ShouldBe(2);
    _upload.ResolvedFileCount.ShouldBe(2);
  }

  private static ParsedFile File(string path) => new(path, [new LineCoverage(1, 1, 0, 0, false, null), new LineCoverage(2, 0, 0, 0, false, null)]);

  private static ProcessUploadCommand Command(Upload upload) => new(upload.PublicId, Worker);

  private ProcessUploadCommand Command() => Command(_upload);

  private ProcessUploadCommandHandler Handler()
  {
    var coverage = A.Fake<IOptionsMonitor<CoverageOptions>>();
    A.CallTo(() => coverage.CurrentValue).Returns(new CoverageOptions());

    var processing = A.Fake<IOptionsMonitor<ProcessingOptions>>();
    A.CallTo(() => processing.CurrentValue).Returns(new ProcessingOptions());

    var resolver = new ConfigResolver(_configs, _gateways, _configParser, _organizationSettings, _clock);
    var trees = new RepositoryTreeProvider(_trees, _gateways, _clock);
    var processor = new UploadProcessor(_raw, _catalog, new ParseGate(processing), coverage);
    var detector = new MergeCommitDetector(_gateways, new CommitAncestry(_gitCommits, _gateways, _clock));

    return new ProcessUploadCommandHandler(_uploads, _commits, _repositories, _directory, resolver, trees, processor, detector, _codec, _reports, _clock);
  }

  private async IAsyncEnumerable<UploadPart> Parts()
  {
    await Task.CompletedTask;

    foreach (var part in _parts)
    {
      yield return part;
    }
  }

  private void Report(string name) => _parts.Add(new UploadPart(UploadPartKind.Report, name, new MemoryStream("<coverage/>"u8.ToArray())));

  private void Network(string text) => _parts.Add(new UploadPart(UploadPartKind.Network, "network.txt", new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text))));

  private void Parses(params ParsedFile[] files) =>
    A.CallTo(() => _parser.ParseAsync(A<Stream>._, A<CancellationToken>._)).Returns(new ParsedReport(ReportFormat.Cobertura, [], files));

  private void Tree(params string[] paths) => Tree(false, paths);

  private void Tree(bool truncated, params string[] paths) =>
    A.CallTo(() => _gateway.GetTreeAsync(A<ForgeRepositoryRef>._, Sha, true, A<CancellationToken>._))
     .Returns(new ForgeTree(Sha, [.. paths.Select(static p => new ForgeTreeEntry(p, GitSha.Parse(new string('b', GitSha.Length)), false))], truncated));

  #endregion
}
