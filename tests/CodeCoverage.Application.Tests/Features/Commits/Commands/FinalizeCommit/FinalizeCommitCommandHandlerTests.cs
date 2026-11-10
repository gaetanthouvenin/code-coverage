// ------------------------------------------------------------------------------------------------
// <copyright file="FinalizeCommitCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Commands.FinalizeCommit;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
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

  private readonly ICoverageConfigParser _configParser = A.Fake<ICoverageConfigParser>();

  private readonly IOrganizationSettingsProvider _organizationSettings = A.Fake<IOrganizationSettingsProvider>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly ICoverageReportCodec _codec = A.Fake<ICoverageReportCodec>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly Dictionary<long, CoverageReport> _coverage = [];

  private readonly Commit _commit;

  private IReadOnlyList<Session> _savedSessions = [];

  private IReadOnlyList<CommitFlagTotals> _savedTotals = [];

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
    A.CallTo(() => _reports.SaveFinalizedAsync(A<Commit>._, A<byte[]>._, A<byte[]>._, A<IReadOnlyList<Session>>._, A<IReadOnlyList<CommitFlagTotals>>._, A<CancellationToken>._))
     .Invokes((Commit _, byte[] _, byte[] _, IReadOnlyList<Session> sessions, IReadOnlyList<CommitFlagTotals> totals, CancellationToken _) =>
       {
         _savedSessions = sessions;
         _savedTotals = totals;
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

  private FinalizeCommitCommandHandler Handler()
  {
    var resolver = new ConfigResolver(_configs, _gateways, _configParser, _organizationSettings, _clock);

    return new FinalizeCommitCommandHandler(_commits, _uploads, _repositories, _directory, resolver, _reports, _codec, new CommitMerger(), _clock);
  }

  #endregion
}
