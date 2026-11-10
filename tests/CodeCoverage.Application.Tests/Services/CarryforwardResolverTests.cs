// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardResolverTests.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Services;

public sealed class CarryforwardResolverTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

  private static readonly ForgeRepositoryRef Address = new(Forge.GitHub, "77", "2002");

  private static readonly GitSha HeadSha = Sha('a');

  private static readonly GitSha ParentSha = Sha('b');

  private static readonly GitSha GrandparentSha = Sha('c');

  private static readonly GitSha OriginSha = Sha('d');

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly IDiffCacheRepository _diffCache = A.Fake<IDiffCacheRepository>();

  private readonly IRepositoryTreeRepository _trees = A.Fake<IRepositoryTreeRepository>();

  private readonly IGitCommitRepository _gitCommits = A.Fake<IGitCommitRepository>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly ICoverageReportCodec _codec = A.Fake<ICoverageReportCodec>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly IOptionsMonitor<ProcessingOptions> _options = A.Fake<IOptionsMonitor<ProcessingOptions>>();

  private readonly Commit _head;

  private long _nextId = 100;

  #endregion

  #region Ctors

  public CarryforwardResolverTests()
  {
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _options.CurrentValue).Returns(new ProcessingOptions { CarryforwardWaitMinutes = 10, CarryforwardMaxDepth = 5 });
    A.CallTo(() => _gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => _diffCache.FindAsync(A<long>._, A<GitSha>._, A<GitSha>._, A<CodeCoverage.Domain.Diffs.DiffSource>._, A<CancellationToken>._))
     .Returns(new CodeCoverage.Domain.Diffs.DiffSet([], false, CodeCoverage.Domain.Diffs.DiffSource.Compare));
    A.CallTo(() => _codec.Encode(A<CoverageReport>._)).Returns([9]);

    _head = Commit.Create(5, HeadSha, Now.AddMinutes(-1));
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_head, 9L);
    _head.MarkPending(null, null, Now.AddMinutes(-1));

    Chain((HeadSha, [ParentSha]), (ParentSha, [GrandparentSha]), (GrandparentSha, [OriginSha]), (OriginSha, []));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  private static CoverageConfig CarriedFlag => CoverageConfig.Create(ConfigNode.FromMap(("flags", ConfigNode.FromMap(("back", ConfigNode.FromMap(("carryforward", ConfigNode.FromBoolean(true))))))));

  #endregion

  #region Methods

  [Fact]
  public async Task Resolve_NoCarriedFlag_ShouldNotReadTheAncestors()
  {
    var resolution = await Resolver().ResolveAsync(_head, Address, CoverageConfig.Create(ConfigNode.FromMap()), [], Ct);

    resolution.Status.ShouldBe(CarryforwardStatus.None);
    A.CallTo(_gitCommits).MustNotHaveHappened();
  }

  [Fact]
  public async Task Resolve_FlagUploaded_ShouldNotReadTheAncestors()
  {
    var resolution = await Resolver().ResolveAsync(_head, Address, CarriedFlag, ["back"], Ct);

    resolution.Status.ShouldBe(CarryforwardStatus.None);
  }

  [Fact]
  public async Task Resolve_NearestCompleteAncestor_ShouldBeOrigin()
  {
    Absent(ParentSha);
    Complete(GrandparentSha, Uploaded(["back"], 11));
    Complete(OriginSha, Uploaded(["back"], 12));

    var resolution = await Resolver().ResolveAsync(_head, Address, CarriedFlag, [], Ct);

    resolution.Status.ShouldBe(CarryforwardStatus.Applied);
    var session = resolution.Sessions.ShouldHaveSingleItem().Session;

    session.Kind.ShouldBe(SessionKind.CarriedForward);
    session.DataUploadId.ShouldBe(11);
    session.CarriedForwardFromSha.ShouldBe(GrandparentSha);
    session.Number.ShouldBe(1);
  }

  [Fact]
  public async Task Resolve_NumbersShouldFollowTheUploadSequence()
  {
    typeof(Commit).GetProperty(nameof(Commit.UploadSequence))!.SetValue(_head, 4);
    Complete(ParentSha, Uploaded(["back"], 11));

    var resolution = await Resolver().ResolveAsync(_head, Address, CarriedFlag, [], Ct);

    resolution.Sessions.ShouldHaveSingleItem().Session.Number.ShouldBe(5);
  }

  [Fact]
  public async Task Resolve_PendingAncestorWithinWait_ShouldThrowRetryLater()
  {
    Pending(ParentSha);

    var exception = await Should.ThrowAsync<RetryLaterException>(() => Resolver().ResolveAsync(_head, Address, CarriedFlag, [], Ct));

    exception.Delay.ShouldBe(TimeSpan.FromSeconds(30));
  }

  [Fact]
  public async Task Resolve_PendingAncestorAfterWait_ShouldUseNextAndMarkAncestorPending()
  {
    Pending(ParentSha);
    _head.MarkPending(null, null, Now.AddMinutes(-11));
    Complete(GrandparentSha, Uploaded(["back"], 11));

    var resolution = await Resolver().ResolveAsync(_head, Address, CarriedFlag, [], Ct);

    resolution.Status.ShouldBe(CarryforwardStatus.AncestorPending);
    resolution.Sessions.ShouldHaveSingleItem().Session.CarriedForwardFromSha.ShouldBe(GrandparentSha);
  }

  [Fact]
  public async Task Resolve_TransitiveCopy_ShouldKeepTheFirstOrigin()
  {
    var transitive = Saved(Session.CarriedForward(_nextId, 1, OriginSha, 50, 11, null, ["back"], "CF[1] - x", null, null, Totals()));
    Complete(ParentSha, transitive);

    var resolution = await Resolver().ResolveAsync(_head, Address, CarriedFlag, [], Ct);

    var session = resolution.Sessions.ShouldHaveSingleItem().Session;

    session.CarriedForwardFromSha.ShouldBe(OriginSha);
    session.Name.ShouldBe("CF[2] - x");
  }

  [Fact]
  public async Task Resolve_NothingOnTheChain_ShouldBeNotFound()
  {
    Absent(ParentSha);
    Absent(GrandparentSha);
    Absent(OriginSha);

    (await Resolver().ResolveAsync(_head, Address, CarriedFlag, [], Ct)).Status.ShouldBe(CarryforwardStatus.NotFound);
  }

  [Fact]
  public async Task Resolve_OnlyCarriesForwardDeclaredFlagAndDefaultRule_ShouldCopyAnyFlag()
  {
    var config = CoverageConfig.Create(ConfigNode.FromMap(("flag_management", ConfigNode.FromMap(("default_rules", ConfigNode.FromMap(("carryforward", ConfigNode.FromBoolean(true))))))));
    Complete(ParentSha, Uploaded(["front"], 11));
    Absent(GrandparentSha);
    Absent(OriginSha);

    var resolution = await Resolver().ResolveAsync(_head, Address, config, [], Ct);

    resolution.Sessions.ShouldHaveSingleItem().Session.Flags.ShouldBe(["front"]);
  }

  [Fact]
  public async Task Resolve_MaxDepth_ShouldBeTheSetting()
  {
    A.CallTo(() => _options.CurrentValue).Returns(new ProcessingOptions { CarryforwardMaxDepth = 1 });
    Complete(ParentSha, Uploaded(["back"], 11));

    await Resolver().ResolveAsync(_head, Address, CarriedFlag, [], Ct);

    A.CallTo(() => _commits.FindByShaAsync(A<long>._, GrandparentSha, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Codecov (get_paths_from_flags): a flag without paths covers every path, so the union of the paths is every path.
  [Fact]
  public async Task Resolve_FlagWithoutPathsNextToAFlagWithPaths_ShouldKeepEveryFile()
  {
    var config = CoverageConfig.Create(
      ConfigNode.FromMap(
        (
          "flags",
          ConfigNode.FromMap(
            ("back", ConfigNode.FromMap(("carryforward", ConfigNode.FromBoolean(true)), ("paths", ConfigNode.FromList(ConfigNode.FromText("src/back/"))))),
            ("tools", ConfigNode.FromMap(("carryforward", ConfigNode.FromBoolean(true))))
          )
        )
      )
    );
    Complete(ParentSha, Uploaded(["back"], 11), Uploaded(["tools"], 12));
    var report = CoverageReport.Create(
      [
        FileCoverage.Create("src/back/A.cs", [new LineCoverage(1, 1, 0, 0, false, null)]),
        FileCoverage.Create("scripts/x.py", [new LineCoverage(1, 1, 0, 0, false, null)]),
      ]
    );
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._)).Returns(report);

    var resolution = await Resolver().ResolveAsync(_head, Address, config, [], Ct);

    var tools = resolution.Sessions.Single(static s => s.Session.Flags.SequenceEqual(["tools"]));
    tools.Report.Files.Select(static f => f.Path).ShouldBe(["scripts/x.py", "src/back/A.cs"], ignoreOrder: true);
  }

  [Fact]
  public async Task Resolve_PendingAncestorAfterWait_ShouldListIt()
  {
    Pending(ParentSha);
    _head.MarkPending(null, null, Now.AddMinutes(-11));
    Absent(GrandparentSha);
    Absent(OriginSha);

    var resolution = await Resolver().ResolveAsync(_head, Address, CarriedFlag, [], Ct);

    resolution.Status.ShouldBe(CarryforwardStatus.AncestorPending);
    resolution.PendingAncestorIds.ShouldBe([70]);
  }

  // The order of the chain is the order of the locks: the nearest ancestor first, whatever the identifiers.
  [Fact]
  public async Task Resolve_SeveralPendingAncestors_ShouldListThemTheNearestFirst()
  {
    Pending(ParentSha, 90);
    Pending(GrandparentSha, 80);
    _head.MarkPending(null, null, Now.AddMinutes(-11));
    Absent(OriginSha);

    var resolution = await Resolver().ResolveAsync(_head, Address, CarriedFlag, [], Ct);

    resolution.PendingAncestorIds.ShouldBe([90, 80]);
  }

  private static GitSha Sha(char c) => GitSha.Parse(new string(c, GitSha.Length));

  private static CoverageTotals Totals() => new(1, 0, 0, 0, 0);

  private CarryforwardResolver Resolver()
  {
    var diffs = new DiffProvider(_diffCache, _gateways, _clock);
    var trees = new RepositoryTreeProvider(_trees, _gateways, _clock);
    var ancestry = new CommitAncestry(_gitCommits, _gateways, _clock);

    return new CarryforwardResolver(ancestry, _commits, _reports, diffs, trees, _codec, _options, _clock, NullLogger<CarryforwardResolver>.Instance);
  }

  private void Chain(params (GitSha Sha, GitSha[] Parents)[] links)
  {
    foreach (var (sha, parents) in links)
    {
      A.CallTo(() => _gitCommits.FindAsync(5, sha, A<CancellationToken>._))
       .Returns(GitCommit.Create(5, new ForgeCommitInfo(sha, parents, "message", null, Now), Now));
    }
  }

  private Session Uploaded(string[] flags, long uploadId) => Saved(Session.FromUpload(++_nextId, 1, uploadId, flags, null, null, null, Totals()));

  private Session Saved(Session session)
  {
    typeof(Session).GetProperty(nameof(Session.Id))!.SetValue(session, ++_nextId);

    return session;
  }

  private void Complete(GitSha sha, params Session[] sessions)
  {
    var ancestor = Commit.Create(5, sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(ancestor, ++_nextId);
    ancestor.RecordFinalization(CommitState.Complete, 0, default, 0, 0, CarryforwardStatus.None, "source", Now);
    A.CallTo(() => _commits.FindByShaAsync(5, sha, A<CancellationToken>._)).Returns(ancestor);
    A.CallTo(() => _reports.ListSessionsAsync(ancestor.Id, A<CancellationToken>._)).Returns(sessions);

    var report = CoverageReport.Create([FileCoverage.Create("src/A.cs", [new LineCoverage(1, 1, 0, 0, false, null)])]);

    A.CallTo(() => _reports.GetUploadCoverageAsync(A<long>._, A<CancellationToken>._)).Returns(new byte[] { 1 });
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._)).Returns(report);
  }

  private void Pending(GitSha sha, long id = 70)
  {
    var ancestor = Commit.Create(5, sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(ancestor, id);
    ancestor.MarkPending(null, null, Now);
    A.CallTo(() => _commits.FindByShaAsync(5, sha, A<CancellationToken>._)).Returns(ancestor);
  }

  private void Absent(GitSha sha) => A.CallTo(() => _commits.FindByShaAsync(5, sha, A<CancellationToken>._)).Returns((Commit?)null);

  #endregion
}
