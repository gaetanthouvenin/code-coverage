// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonBuilderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Services;

public sealed class ComparisonBuilderTests
{
  #region Fields

  private const long RepositoryId = 1;

  private const long HeadId = 100;

  private const long BaseId = 200;

  private const string TwoLinesAdded = "@@ -1,2 +1,4 @@\n a\n+x\n+y\n b";

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Head = Sha('a');

  private static readonly GitSha BranchTip = Sha('c');

  private static readonly GitSha ForkPoint = Sha('d');

  private static readonly GitSha Parent = Sha('e');

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IGitCommitRepository _gitCommits = A.Fake<IGitCommitRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly ICoverageReportCodec _codec = A.Fake<ICoverageReportCodec>();

  private readonly IComparisonRepository _comparisons = A.Fake<IComparisonRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly Dictionary<byte, CoverageReport> _blobs = [];

  private readonly TrackedRepository _repository = TrackedRepository.Create(42, "7", RepositorySlug.Parse("octo/app"), "main", false, "badge", Now);

  private readonly ComparisonBuilder _builder;

  private CommitComparison? _saved;

  #endregion

  #region Ctors

  public ComparisonBuilderTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    var clock = A.Fake<IClock>();
    var monitor = A.Fake<IOptionsMonitor<ProcessingOptions>>();
    var diffCache = A.Fake<IDiffCacheRepository>();
    var views = A.Fake<IReportViewCache>();

    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => monitor.CurrentValue).Returns(new ProcessingOptions());
    A.CallTo(() => diffCache.FindAsync(A<long>._, A<GitSha>._, A<GitSha>._, A<DiffSource>._, A<CancellationToken>._)).Returns((DiffSet?)null);
    A.CallTo(() => views.GetOrCreate(A<long>._, A<int>._, A<IReadOnlyList<SessionReport>>._, A<IReadOnlyCollection<string>>._, A<PathPatternSet>._))
     .ReturnsLazily((long _, int _, IReadOnlyList<SessionReport> sessions, IReadOnlyCollection<string> flags, PathPatternSet paths) => ReportView.Build(sessions, flags, paths));
    A.CallTo(() => _pullRequests.FindOpenByHeadShaAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns((PullRequest?)null);
    A.CallTo(() => _commits.FindByShaAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns((Commit?)null);
    A.CallTo(() => _gitCommits.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns((GitCommit?)null);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).Returns((ForgePullInfo?)null);
    A.CallTo(() => _gateway.CompareAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<GitSha>._, A<CancellationToken>._)).Returns((ForgeCompareResult?)null);
    A.CallTo(() => _gateway.ListPullFilesAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).Returns((ForgePullFiles?)null);
    A.CallTo(() => _gateway.ListCommitsAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<int>._, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._)).ReturnsLazily((ReadOnlyMemory<byte> blob) => _blobs[blob.Span[0]]);
    A.CallTo(() => _reports.GetUploadCoverageAsync(A<long>._, A<CancellationToken>._)).Returns((byte[]?)null);
    A.CallTo(() => _comparisons.SaveAsync(A<CommitComparison>._, A<CancellationToken>._))
     .Invokes((CommitComparison comparison, CancellationToken _) => _saved = comparison);

    var resolver = new BaseResolver(
      _pullRequests,
      _commits,
      new CommitAncestry(_gitCommits, gateways, clock),
      new DiffProvider(diffCache, gateways, clock),
      gateways,
      monitor,
      clock
    );

    _builder = new ComparisonBuilder(resolver, new CommitReportReader(_reports, _codec), views, _comparisons, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Build_ShouldUsePatchDiffForPatchAndComparisonDiffForIndirect()
  {
    // The fork point has no report: the base is its parent, one commit before, so the two diffs differ.
    PullOf(Head);
    Compared(BranchTip, Head, ForkPoint, "ignored.cs");
    PullFiles(false, Changed("src/a.cs", TwoLinesAdded));
    Git(ForkPoint, Parent);
    BaseCommit(Parent);
    Compared(Parent, Head, Parent, "src/a.cs", "src/b.cs");
    Sessions(HeadId, 1, Report(File("src/a.cs", (2, 1), (3, 0)), File("src/b.cs", (1, 0)), File("src/c.cs", (1, 0))));
    Sessions(BaseId, 2, Report(File("src/a.cs", (1, 1)), File("src/b.cs", (1, 1)), File("src/c.cs", (1, 1))));

    var context = await _builder.BuildAsync(_repository, Forge.GitHub, HeadCommit(), Config(), Ct);

    var result = context.Comparison.Result;
    // The patch: the two lines the pull adds to a.cs.
    result.Patch.Totals.ShouldBe(new CoverageTotals(1, 1, 0, 0, 0));
    result.Files.ShouldHaveSingleItem().Path.ShouldBe("src/a.cs");
    // The indirect changes: b.cs changed between the base and the fork point, so it is in the comparison diff
    // and left out; with the patch diff it would be listed as an indirect change of this pull.
    result.IndirectChanges.ShouldHaveSingleItem().Path.ShouldBe("src/c.cs");
    result.IndirectChanges[0].IsFailing.ShouldBeTrue();
    result.Head.ShouldBe(new CoverageTotals(1, 3, 0, 0, 0));
    result.Base.ShouldBe(new CoverageTotals(3, 0, 0, 0, 0));
    result.Delta.ShouldBe(-75m);
    result.DiffIncomplete.ShouldBeFalse();
  }

  [Fact]
  public async Task Build_ShouldStoreWhatWasCompared()
  {
    PullOf(Head);
    Compared(BranchTip, Head, ForkPoint);
    PullFiles(false, Changed("src/a.cs", TwoLinesAdded));
    Git(ForkPoint, Parent);
    BaseCommit(Parent);
    Compared(Parent, Head, Parent, "src/a.cs");
    Sessions(HeadId, 1, Report(File("src/a.cs", (2, 1))));
    Sessions(BaseId, 2, Report(File("src/a.cs", (1, 1))));

    var context = await _builder.BuildAsync(_repository, Forge.GitHub, HeadCommit(), Config(), Ct);

    var saved = _saved.ShouldNotBeNull();
    saved.ShouldBeSameAs(context.Comparison);
    saved.CommitId.ShouldBe(HeadId);
    saved.PullNumber.ShouldBe(12);
    saved.ForkPointSha.ShouldBe(ForkPoint);
    saved.ComparedToSha.ShouldBe(Parent);
    saved.HeadRevision.ShouldBe(1);
    saved.BaseRevision.ShouldBe(1);
    saved.DiffSource.ShouldBe(DiffSource.PullFiles);
    saved.ComputedAt.ShouldBe(Now);
    context.Resolution.Hops.ShouldBe(1);
    saved.Hops.ShouldBe(1);
    saved.BehindBy.ShouldBe(context.Resolution.BehindBy);
    context.HeadSessions.ShouldHaveSingleItem();
    context.BaseSessions.ShouldNotBeNull().ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Build_TruncatedDiff_ShouldPropagateDiffIncomplete()
  {
    PullOf(Head);
    Compared(BranchTip, Head, ForkPoint);
    PullFiles(true, Changed("src/a.cs", TwoLinesAdded));
    BaseCommit(ForkPoint);
    Sessions(HeadId, 1, Report(File("src/a.cs", (2, 1))));
    Sessions(BaseId, 2, Report(File("src/a.cs", (1, 1))));

    var context = await _builder.BuildAsync(_repository, Forge.GitHub, HeadCommit(), Config(), Ct);

    context.Comparison.Result.DiffIncomplete.ShouldBeTrue();
    _saved.ShouldNotBeNull().Result.DiffIncomplete.ShouldBeTrue();
  }

  [Fact]
  public async Task Build_PushBuildWithoutBase_ShouldCompareWithNothing()
  {
    Git(Head);
    Sessions(HeadId, 1, Report(File("src/a.cs", (1, 1), (2, 0))));

    var context = await _builder.BuildAsync(_repository, Forge.GitHub, HeadCommit(), Config(), Ct);

    var result = context.Comparison.Result;
    result.Base.ShouldBeNull();
    result.Delta.ShouldBeNull();
    result.Patch.Coverage.ShouldBeNull();
    result.IndirectChanges.ShouldBeEmpty();
    result.Head.ShouldBe(new CoverageTotals(1, 1, 0, 0, 0));
    context.BaseSessions.ShouldBeNull();
    _saved.ShouldNotBeNull().PullNumber.ShouldBeNull();
    _saved.ComparedToSha.ShouldBeNull();
    _saved.BaseRevision.ShouldBeNull();
  }

  // The report of the base went away since it was chosen: no base, instead of a comparison with a part of it.
  [Fact]
  public async Task Build_BaseReportNoLongerStored_ShouldCompareWithNothing()
  {
    Git(Head, Parent);
    BaseCommit(Parent);
    Compared(Parent, Head, Parent, "src/a.cs");
    Sessions(HeadId, 1, Report(File("src/a.cs", (1, 1))));
    A.CallTo(() => _reports.ListSessionsAsync(BaseId, A<CancellationToken>._)).Returns([Session.FromUpload(BaseId, 1, 99, [], null, null, null, default)]);

    var context = await _builder.BuildAsync(_repository, Forge.GitHub, HeadCommit(), Config(), Ct);

    context.Comparison.Result.Base.ShouldBeNull();
    context.Comparison.ComparedToSha.ShouldBeNull();
    context.Resolution.ComparedTo.ShouldBeNull();
    context.Comparison.Result.IndirectChanges.ShouldBeEmpty();
    context.Comparison.Hops.ShouldBeNull();
  }

  [Fact]
  public async Task Build_HeadReportNoLongerStored_ShouldThrowUnprocessableAndStoreNothing()
  {
    Git(Head);
    A.CallTo(() => _reports.ListSessionsAsync(HeadId, A<CancellationToken>._)).Returns([Session.FromUpload(HeadId, 1, 99, [], null, null, null, default)]);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _builder.BuildAsync(_repository, Forge.GitHub, HeadCommit(), Config(), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.Compacted);
    A.CallTo(_comparisons).MustNotHaveHappened();
  }

  [Fact]
  public async Task Build_ShouldCompareEachFlagAndMarkTheCarriedForwardOnes()
  {
    Git(Head, Parent);
    BaseCommit(Parent);
    Compared(Parent, Head, Parent, "src/a.cs");
    _blobs[1] = Report(File("src/a.cs", (2, 1), (3, 0)));
    _blobs[3] = Report(File("api/b.cs", (1, 1)));
    A.CallTo(() => _reports.ListSessionsAsync(HeadId, A<CancellationToken>._))
     .Returns(
       [
         Session.FromUpload(HeadId, 1, 1, ["unit"], null, null, null, default),
         Session.CarriedForward(HeadId, 2, Parent, 77, 3, null, ["api"], null, null, null, default),
       ]
     );
    A.CallTo(() => _reports.GetUploadCoverageAsync(1, A<CancellationToken>._)).Returns([1]);
    A.CallTo(() => _reports.GetUploadCoverageAsync(3, A<CancellationToken>._)).Returns([3]);
    Sessions(BaseId, 2, Report(File("src/a.cs", (1, 1))));

    var context = await _builder.BuildAsync(_repository, Forge.GitHub, HeadCommit(), Config(), Ct);

    var flags = context.Comparison.Result.Flags;
    flags.Select(static f => (f.Flag, f.CarriedForward)).ShouldBe([("api", true), ("unit", false)]);
    flags[1].Head.ShouldBe(new CoverageTotals(1, 1, 0, 0, 0));
    flags[0].Base.ShouldBeNull();
  }

  // The project view keeps the paths the configuration does not ignore, on both sides.
  [Fact]
  public async Task Build_IgnoredPaths_ShouldLeaveBothReports()
  {
    Git(Head, Parent);
    BaseCommit(Parent);
    Compared(Parent, Head, Parent);
    Sessions(HeadId, 1, Report(File("src/a.cs", (1, 1)), File("gen/x.cs", (1, 0))));
    Sessions(BaseId, 2, Report(File("src/a.cs", (1, 1)), File("gen/x.cs", (1, 1))));

    var context = await _builder.BuildAsync(_repository, Forge.GitHub, HeadCommit(), Config(("ignore", ConfigNode.FromList(ConfigNode.FromText("gen/")))), Ct);

    context.Comparison.Result.Head.ShouldBe(new CoverageTotals(1, 0, 0, 0, 0));
    context.Comparison.Result.Base.ShouldBe(new CoverageTotals(1, 0, 0, 0, 0));
    context.Comparison.Result.IndirectChanges.ShouldBeEmpty();
  }

  [Fact]
  public async Task Build_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _builder.BuildAsync(null!, Forge.GitHub, HeadCommit(), Config(), Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _builder.BuildAsync(_repository, Forge.GitHub, null!, Config(), Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _builder.BuildAsync(_repository, Forge.GitHub, HeadCommit(), null!, Ct));
  }

  private static GitSha Sha(char digit) => GitSha.Parse(new string(digit, GitSha.Length));

  private static FileCoverage File(string path, params (int Line, long Hits)[] lines) =>
    FileCoverage.Create(path, lines.Select(static line => new LineCoverage(line.Line, line.Hits, 0, 0, false, null)));

  private static CoverageReport Report(params FileCoverage[] files) => CoverageReport.Create(files);

  private static ForgeChangedFile Changed(string path, string? patch = "@@ -1 +1 @@\n-a\n+b") => new(path, null, ForgeChangedFileStatus.Modified, patch);

  private static CoverageConfig Config(params (string Key, ConfigNode Value)[] entries) => CoverageConfig.Create(ConfigNode.FromMap(entries));

  private static Commit Complete(long id, GitSha sha)
  {
    var commit = Commit.Create(RepositoryId, sha, Now.AddHours(-2));

    commit.MarkPending("topic", null, Now.AddHours(-2));
    commit.RecordFinalization(CommitState.Complete, 1, default, 1, 1, CarryforwardStatus.None, "source", Now.AddHours(-1));
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, id);

    return commit;
  }

  private static Commit HeadCommit() => Complete(HeadId, Head);

  private void BaseCommit(GitSha sha)
  {
    A.CallTo(() => _commits.FindByShaAsync(A<long>._, sha, A<CancellationToken>._)).Returns(Complete(BaseId, sha));
  }

  private void PullOf(GitSha head)
  {
    var pull = PullRequest.Create(RepositoryId, new ForgePullInfo(12, ForgePullState.Open, "A pull", "octocat", head, "topic", "main", BranchTip), Now);

    A.CallTo(() => _pullRequests.FindOpenByHeadShaAsync(A<long>._, head, A<CancellationToken>._)).Returns(pull);
  }

  private void Compared(GitSha baseSha, GitSha headSha, GitSha mergeBase, params string[] files)
  {
    A.CallTo(() => _gateway.CompareAsync(A<ForgeRepositoryRef>._, baseSha, headSha, A<CancellationToken>._))
     .Returns(new ForgeCompareResult(mergeBase, 1, 0, [.. files.Select(static f => Changed(f))], Truncated: false));
  }

  private void PullFiles(bool truncated, params ForgeChangedFile[] files)
  {
    A.CallTo(() => _gateway.ListPullFilesAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._)).Returns(new ForgePullFiles(files, truncated));
  }

  private void Git(GitSha sha, params GitSha[] parents)
  {
    A.CallTo(() => _gitCommits.FindAsync(A<long>._, sha, A<CancellationToken>._))
     .Returns(GitCommit.Create(RepositoryId, new ForgeCommitInfo(sha, parents, "message", null, Now), Now));
  }

  // One uploaded session, whose blob is the single byte that names its report.
  private void Sessions(long commitId, byte blob, CoverageReport report)
  {
    _blobs[blob] = report;
    A.CallTo(() => _reports.ListSessionsAsync(commitId, A<CancellationToken>._)).Returns([Session.FromUpload(commitId, 1, blob, [], null, null, null, report.Totals)]);
    A.CallTo(() => _reports.GetUploadCoverageAsync(blob, A<CancellationToken>._)).Returns([blob]);
  }

  #endregion
}