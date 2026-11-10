// ------------------------------------------------------------------------------------------------
// <copyright file="BaseResolverTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Services;

public sealed class BaseResolverTests
{
  #region Fields

  private const long RepositoryId = 1;

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Head = Sha('a');

  private static readonly GitSha NewerHead = Sha('b');

  private static readonly GitSha BranchTip = Sha('c');

  private static readonly GitSha ForkPoint = Sha('d');

  private static readonly GitSha Parent = Sha('e');

  private static readonly GitSha GrandParent = Sha('f');

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IGitCommitRepository _gitCommits = A.Fake<IGitCommitRepository>();

  private readonly IDiffCacheRepository _diffCache = A.Fake<IDiffCacheRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly ProcessingOptions _options = new();

  private readonly Dictionary<GitSha, GitCommit> _git = [];

  private readonly TrackedRepository _repository = TrackedRepository.Create(42, "7", RepositorySlug.Parse("octo/app"), "main", false, "badge", Now);

  private readonly BaseResolver _resolver;

  #endregion

  #region Ctors

  public BaseResolverTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    var clock = A.Fake<IClock>();
    var monitor = A.Fake<IOptionsMonitor<ProcessingOptions>>();

    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => monitor.CurrentValue).Returns(_options);
    A.CallTo(() => _pullRequests.FindOpenByHeadShaAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns((PullRequest?)null);
    A.CallTo(() => _commits.FindByShaAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).Returns((Commit?)null);
    A.CallTo(() => _diffCache.FindAsync(A<long>._, A<GitSha>._, A<GitSha>._, A<DiffSource>._, A<CancellationToken>._)).Returns((DiffSet?)null);
    A.CallTo(() => _gitCommits.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._))
     .ReturnsLazily((long _, GitSha sha, CancellationToken _) => _git.GetValueOrDefault(sha));
    A.CallTo(() => _gitCommits.AddMissingAsync(A<long>._, A<IReadOnlyCollection<ForgeCommitInfo>>._, A<DateTimeOffset>._, A<CancellationToken>._))
     .Invokes(
       (long _, IReadOnlyCollection<ForgeCommitInfo> page, DateTimeOffset at, CancellationToken _) =>
       {
         foreach (var info in page)
         {
           _git[info.Sha] = GitCommit.Create(RepositoryId, info, at);
         }
       }
     );
    A.CallTo(() => _gateway.ListCommitsAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<int>._, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => _gateway.CompareAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<GitSha>._, A<CancellationToken>._)).Returns((ForgeCompareResult?)null);
    A.CallTo(() => _gateway.ListPullFilesAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).Returns((ForgePullFiles?)null);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).Returns((ForgePullInfo?)null);

    _resolver = new BaseResolver(
      _pullRequests,
      _commits,
      new CommitAncestry(_gitCommits, gateways, clock),
      new DiffProvider(_diffCache, gateways, clock),
      gateways,
      monitor,
      clock
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Resolve_PrHead_ShouldUsePullFiles()
  {
    var pull = OpenPull(head: Head);
    Compared(BranchTip, Head, mergeBase: ForkPoint, behindBy: 3, "from-compare.cs");
    PullFiles(12, "from-pull.cs");
    var forkPoint = Reported(ForkPoint);

    var resolution = await ResolveAsync(HeadCommit());

    resolution.PullRequest.ShouldBe(pull);
    resolution.ForkPoint.ShouldBe(ForkPoint);
    resolution.ComparedTo.ShouldBe(forkPoint);
    resolution.BehindBy.ShouldBe(3);
    resolution.Hops.ShouldBe(0);
    resolution.IsExactBase.ShouldBeTrue();
    resolution.PatchDiff.Source.ShouldBe(DiffSource.PullFiles);
    resolution.PatchDiff.Files.ShouldHaveSingleItem().Path.ShouldBe("from-pull.cs");
    // The base is the fork point: one diff serves the patch, the adjusted base and the indirect changes.
    resolution.ComparisonDiff.ShouldBeSameAs(resolution.PatchDiff);
    resolution.DiffIncomplete.ShouldBeFalse();
    // The merge base comes from one compare of the base of the pull with the commit, never from a local history.
    A.CallTo(() => _gateway.CompareAsync(A<ForgeRepositoryRef>._, BranchTip, Head, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _gateway.CompareAsync(A<ForgeRepositoryRef>._, ForkPoint, Head, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Resolve_PrHeadWithoutPullFiles_ShouldFallBackToTheCompareFromTheForkPoint()
  {
    OpenPull(head: Head);
    Compared(BranchTip, Head, mergeBase: ForkPoint, behindBy: 0);
    Compared(ForkPoint, Head, mergeBase: ForkPoint, behindBy: 0, "from-compare.cs");
    Reported(ForkPoint);

    var resolution = await ResolveAsync(HeadCommit());

    resolution.PatchDiff.Source.ShouldBe(DiffSource.Compare);
    resolution.PatchDiff.Files.ShouldHaveSingleItem().Path.ShouldBe("from-compare.cs");
  }

  [Fact]
  public async Task Resolve_StaleHead_ShouldCompareForkPointToSha()
  {
    // The pull moved on: its files describe the newer head, not this commit.
    OpenPull(head: NewerHead, findByHead: false);
    Compared(BranchTip, Head, mergeBase: ForkPoint, behindBy: 1);
    Compared(ForkPoint, Head, mergeBase: ForkPoint, behindBy: 0, "of-this-sha.cs");
    PullFiles(12, "of-the-newer-head.cs");
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._)).Returns(PullInfo(NewerHead));
    Reported(ForkPoint);

    var resolution = await ResolveAsync(HeadCommit(pullNumber: 12));

    resolution.PullRequest.ShouldNotBeNull().HeadSha.ShouldBe(NewerHead);
    resolution.PatchDiff.Files.ShouldHaveSingleItem().Path.ShouldBe("of-this-sha.cs");
    A.CallTo(() => _gateway.ListPullFilesAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Resolve_PushBuild_ShouldUseNearestReportedAncestor()
  {
    Git(Head, Parent);
    Git(Parent, GrandParent);
    Git(GrandParent);
    var grandParent = Reported(GrandParent);
    Compared(GrandParent, Head, mergeBase: GrandParent, behindBy: 0, "since-the-report.cs");

    var resolution = await ResolveAsync(HeadCommit());

    resolution.PullRequest.ShouldBeNull();
    resolution.ForkPoint.ShouldBeNull();
    resolution.BehindBy.ShouldBeNull();
    resolution.ComparedTo.ShouldBe(grandParent);
    resolution.Hops.ShouldBe(1);
    resolution.IsExactBase.ShouldBeFalse();
    // The patch of a push build covers every commit that has no report since the base.
    resolution.PatchDiff.Files.ShouldHaveSingleItem().Path.ShouldBe("since-the-report.cs");
    resolution.ComparisonDiff.ShouldBeSameAs(resolution.PatchDiff);
  }

  [Fact]
  public async Task Resolve_PushBuildWithNoLocalHistory_ShouldReadTheParentsFromTheForge()
  {
    A.CallTo(() => _gateway.ListCommitsAsync(A<ForgeRepositoryRef>._, Head, A<int>._, A<CancellationToken>._))
     .Returns([new ForgeCommitInfo(Head, [Parent], "head", null, Now), new ForgeCommitInfo(Parent, [], "parent", null, Now)]);
    var parent = Reported(Parent);
    Compared(Parent, Head, mergeBase: Parent, behindBy: 0, "a.cs");

    var resolution = await ResolveAsync(HeadCommit());

    resolution.ComparedTo.ShouldBe(parent);
    resolution.Hops.ShouldBe(0);
  }

  [Fact]
  public async Task Resolve_PushBuildOnAMergeCommit_ShouldFollowTheFirstParentOnly()
  {
    // A merge commit: the second parent, the merged branch, is never the base.
    Git(Head, Parent, GrandParent);
    Git(Parent);
    Reported(GrandParent);

    var resolution = await ResolveAsync(HeadCommit());

    resolution.ComparedTo.ShouldBeNull();
  }

  [Fact]
  public async Task Resolve_RootCommit_ShouldHaveNoBaseAndAnEmptyCompleteDiff()
  {
    Git(Head);

    var resolution = await ResolveAsync(HeadCommit());

    resolution.ComparedTo.ShouldBeNull();
    resolution.Hops.ShouldBeNull();
    resolution.PatchDiff.Files.ShouldBeEmpty();
    resolution.ComparisonDiff.Files.ShouldBeEmpty();
    resolution.DiffIncomplete.ShouldBeFalse();
    A.CallTo(() => _gateway.CompareAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<GitSha>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Resolve_CommitUnknownToTheForge_ShouldHaveNoBase()
  {
    var resolution = await ResolveAsync(HeadCommit());

    resolution.ComparedTo.ShouldBeNull();
    resolution.PatchDiff.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Resolve_PendingBase_ShouldRetryLater()
  {
    Git(Head, Parent);
    Git(Parent, GrandParent);
    Pending(Parent);
    Reported(GrandParent);

    var exception = await Should.ThrowAsync<RetryLaterException>(() => ResolveAsync(HeadCommit(completedAt: Now.AddMinutes(-5))));

    exception.Delay.ShouldBe(TimeSpan.FromMinutes(1));
  }

  [Fact]
  public async Task Resolve_PendingBaseNearTheEndOfTheWait_ShouldRetryAtItsEnd()
  {
    Git(Head, Parent);
    Pending(Parent);

    var exception = await Should.ThrowAsync<RetryLaterException>(() => ResolveAsync(HeadCommit(completedAt: Now.AddMinutes(-15).AddSeconds(20))));

    exception.Delay.ShouldBe(TimeSpan.FromSeconds(20));
  }

  [Fact]
  public async Task Resolve_PendingBaseAfterTheWait_ShouldWalkPastIt()
  {
    Git(Head, Parent);
    Git(Parent, GrandParent);
    Pending(Parent);
    var grandParent = Reported(GrandParent);

    // Exactly at the end of the wait: nothing is left to wait for.
    var resolution = await ResolveAsync(HeadCommit(completedAt: Now.AddMinutes(-15)));

    resolution.ComparedTo.ShouldBe(grandParent);
    resolution.Hops.ShouldBe(1);
  }

  [Theory]
  [InlineData(CommitState.Known)]
  [InlineData(CommitState.Error)]
  [InlineData(CommitState.Compacted)]
  public async Task Resolve_AncestorWithoutAUsableReport_ShouldBeWalkedPast(CommitState state)
  {
    Git(Head, Parent);
    Git(Parent, GrandParent);
    Stored(Parent, state);
    var grandParent = Reported(GrandParent);

    var resolution = await ResolveAsync(HeadCommit());

    resolution.ComparedTo.ShouldBe(grandParent);
  }

  [Theory]
  [InlineData(1, true)]
  [InlineData(0, false)]
  public async Task Resolve_ShouldStopAtTheMaximumWalk(int maxWalk, bool found)
  {
    _options.BaseMaxAncestorWalk = maxWalk;
    Git(Head, Parent);
    Git(Parent, GrandParent);
    Reported(GrandParent);

    var resolution = await ResolveAsync(HeadCommit());

    (resolution.ComparedTo is not null).ShouldBe(found);
  }

  [Fact]
  public async Task Resolve_PseudoCompareDisabled_ShouldReturnNullComparedTo()
  {
    OpenPull(head: Head);
    Compared(BranchTip, Head, mergeBase: ForkPoint, behindBy: 0);
    PullFiles(12, "from-pull.cs");
    Git(ForkPoint, Parent);
    Reported(Parent);

    var resolution = await ResolveAsync(HeadCommit(), allowPseudoCompare: false);

    resolution.ComparedTo.ShouldBeNull();
    resolution.Hops.ShouldBeNull();
    resolution.ForkPoint.ShouldBe(ForkPoint);
    // The patch does not depend on the base report: it still starts at the fork point.
    resolution.PatchDiff.Files.ShouldHaveSingleItem().Path.ShouldBe("from-pull.cs");
    resolution.ComparisonDiff.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Resolve_PseudoCompareDisabledWithTheExactBase_ShouldKeepIt()
  {
    OpenPull(head: Head);
    Compared(BranchTip, Head, mergeBase: ForkPoint, behindBy: 0);
    PullFiles(12, "from-pull.cs");
    var forkPoint = Reported(ForkPoint);

    var resolution = await ResolveAsync(HeadCommit(), allowPseudoCompare: false);

    resolution.ComparedTo.ShouldBe(forkPoint);
  }

  [Fact]
  public async Task Resolve_PushBuildPseudoCompareDisabled_ShouldOnlyAcceptTheFirstParent()
  {
    Git(Head, Parent);
    Git(Parent, GrandParent);
    Reported(GrandParent);

    var resolution = await ResolveAsync(HeadCommit(), allowPseudoCompare: false);

    resolution.ComparedTo.ShouldBeNull();
    resolution.PatchDiff.Files.ShouldBeEmpty();
  }

  [Fact]
  public async Task Resolve_BaseBeforeTheForkPoint_ShouldReadTheComparisonDiffFromTheBase()
  {
    OpenPull(head: Head);
    Compared(BranchTip, Head, mergeBase: ForkPoint, behindBy: 0);
    PullFiles(12, "from-pull.cs");
    Git(ForkPoint, Parent);
    Git(Parent, GrandParent);
    var grandParent = Reported(GrandParent);
    Compared(GrandParent, Head, mergeBase: GrandParent, behindBy: 0, "from-pull.cs", "before-the-fork.cs");

    var resolution = await ResolveAsync(HeadCommit());

    resolution.ComparedTo.ShouldBe(grandParent);
    resolution.Hops.ShouldBe(2);
    resolution.PatchDiff.Files.Select(static f => f.Path).ShouldBe(["from-pull.cs"]);
    resolution.ComparisonDiff.Files.Select(static f => f.Path).ShouldBe(["from-pull.cs", "before-the-fork.cs"]);
  }

  [Fact]
  public async Task Resolve_PullNumberOfTheUploads_ShouldReadThePullFromTheForgeAndStoreIt()
  {
    var info = PullInfo(Head);
    var stored = PullRequest.Create(RepositoryId, info, Now);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._)).Returns(info);
    A.CallTo(() => _pullRequests.UpsertAsync(A<long>._, info, Now, A<CancellationToken>._)).Returns(stored);
    Compared(BranchTip, Head, mergeBase: ForkPoint, behindBy: 0);
    PullFiles(12, "from-pull.cs");

    var resolution = await ResolveAsync(HeadCommit(pullNumber: 12));

    resolution.PullRequest.ShouldBe(stored);
    resolution.ForkPoint.ShouldBe(ForkPoint);
  }

  [Fact]
  public async Task Resolve_PullNumberUnknownToTheForge_ShouldBeAPushBuild()
  {
    Git(Head, Parent);
    var parent = Reported(Parent);
    Compared(Parent, Head, mergeBase: Parent, behindBy: 0, "a.cs");

    var resolution = await ResolveAsync(HeadCommit(pullNumber: 99));

    resolution.PullRequest.ShouldBeNull();
    resolution.ComparedTo.ShouldBe(parent);
    A.CallTo(() => _pullRequests.UpsertAsync(A<long>._, A<ForgePullInfo>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The stored row is behind the forge (its webhook is late or lost): the files of the pull are those of the
  // newer head, and must never be read, nor cached, as the patch of this commit.
  [Fact]
  public async Task Resolve_StoredPullBehindTheForge_ShouldCompareForkPointToShaInsteadOfThePullFiles()
  {
    OpenPull(head: Head);
    var moved = PullRequest.Create(RepositoryId, PullInfo(NewerHead), Now);
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._)).Returns(PullInfo(NewerHead));
    A.CallTo(() => _pullRequests.UpsertAsync(A<long>._, A<ForgePullInfo>._, A<DateTimeOffset>._, A<CancellationToken>._)).Returns(moved);
    Compared(BranchTip, Head, mergeBase: ForkPoint, behindBy: 0);
    Compared(ForkPoint, Head, mergeBase: ForkPoint, behindBy: 0, "of-this-sha.cs");
    PullFiles(12, "of-the-newer-head.cs");

    var resolution = await ResolveAsync(HeadCommit());

    resolution.PullRequest.ShouldBe(moved);
    resolution.PatchDiff.Files.ShouldHaveSingleItem().Path.ShouldBe("of-this-sha.cs");
    A.CallTo(() => _gateway.ListPullFilesAsync(A<ForgeRepositoryRef>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _diffCache.AddAsync(A<long>._, A<GitSha>._, A<GitSha>._, A<DiffSet>.That.Matches(d => d.Source == DiffSource.PullFiles), A<DateTimeOffset>._, A<CancellationToken>._))
     .MustNotHaveHappened();
  }

  [Fact]
  public async Task Resolve_StoredPullTheForgeNoLongerGives_ShouldKeepTheRow()
  {
    var pull = OpenPull(head: Head);
    Compared(BranchTip, Head, mergeBase: ForkPoint, behindBy: 0);
    PullFiles(12, "from-pull.cs");

    var resolution = await ResolveAsync(HeadCommit());

    resolution.PullRequest.ShouldBe(pull);
    A.CallTo(() => _pullRequests.UpsertAsync(A<long>._, A<ForgePullInfo>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Without pseudo compare, an ancestor beyond the exact base can never be used: it is neither read nor waited for.
  [Fact]
  public async Task Resolve_PseudoCompareDisabledWithAPendingAncestor_ShouldNotWaitForIt()
  {
    Git(Head, Parent);
    Git(Parent, GrandParent);
    Pending(GrandParent);

    var resolution = await ResolveAsync(HeadCommit(completedAt: Now), allowPseudoCompare: false);

    resolution.ComparedTo.ShouldBeNull();
    A.CallTo(() => _commits.FindByShaAsync(A<long>._, GrandParent, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Resolve_PseudoCompareDisabledWithAPendingExactBase_ShouldWaitForIt()
  {
    Git(Head, Parent);
    Pending(Parent);

    await Should.ThrowAsync<RetryLaterException>(() => ResolveAsync(HeadCommit(completedAt: Now), allowPseudoCompare: false));
  }

  [Fact]
  public async Task Resolve_MergeBaseUnavailable_ShouldStartAtTheBaseOfThePullAndMarkTheDiffIncomplete()
  {
    OpenPull(head: Head);
    var tip = Reported(BranchTip);

    var resolution = await ResolveAsync(HeadCommit());

    resolution.ForkPoint.ShouldBe(BranchTip);
    resolution.BehindBy.ShouldBeNull();
    resolution.ComparedTo.ShouldBe(tip);
    resolution.PatchDiff.Files.ShouldBeEmpty();
    resolution.PatchDiff.Truncated.ShouldBeTrue();
    resolution.DiffIncomplete.ShouldBeTrue();
  }

  [Fact]
  public async Task Resolve_TruncatedDiff_ShouldBePropagated()
  {
    OpenPull(head: Head);
    Compared(BranchTip, Head, mergeBase: ForkPoint, behindBy: 0);
    A.CallTo(() => _gateway.ListPullFilesAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Returns(new ForgePullFiles([new ForgeChangedFile("a.cs", null, ForgeChangedFileStatus.Modified, "@@ -1 +1 @@\n-a\n+b")], Truncated: true));
    Reported(ForkPoint);

    var resolution = await ResolveAsync(HeadCommit());

    resolution.DiffIncomplete.ShouldBeTrue();
  }

  [Fact]
  public async Task Resolve_ShouldAddressTheRepositoryByItsInstallationAndIdentifier()
  {
    Git(Head, Parent);
    Reported(Parent);

    await ResolveAsync(HeadCommit());

    A.CallTo(() => _gateway.CompareAsync(new ForgeRepositoryRef(Forge.GitHub, "42", "7"), Parent, Head, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Resolve_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _resolver.ResolveAsync(null!, Forge.GitHub, HeadCommit(), true, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _resolver.ResolveAsync(_repository, Forge.GitHub, null!, true, Ct));
  }

  private static GitSha Sha(char digit) => GitSha.Parse(new string(digit, GitSha.Length));

  private static ForgePullInfo PullInfo(GitSha head) => new(12, ForgePullState.Open, "A pull", "octocat", head, "topic", "main", BranchTip);

  private static Commit HeadCommit(int? pullNumber = null, DateTimeOffset? completedAt = null)
  {
    var commit = Commit.Create(RepositoryId, Head, Now.AddHours(-1));

    commit.MarkPending("topic", pullNumber, Now.AddHours(-1));
    commit.RecordFinalization(CommitState.Complete, 1, default, 1, 1, CarryforwardStatus.None, "source", completedAt ?? Now.AddHours(-1));

    return commit;
  }

  private Task<Domain.Comparisons.BaseResolution> ResolveAsync(Commit commit, bool allowPseudoCompare = true) =>
    _resolver.ResolveAsync(_repository, Forge.GitHub, commit, allowPseudoCompare, Ct);

  private PullRequest OpenPull(GitSha head, bool findByHead = true)
  {
    var pull = PullRequest.Create(RepositoryId, PullInfo(head), Now);

    if (findByHead)
    {
      A.CallTo(() => _pullRequests.FindOpenByHeadShaAsync(A<long>._, head, A<CancellationToken>._)).Returns(pull);
    }
    else
    {
      A.CallTo(() => _pullRequests.UpsertAsync(A<long>._, A<ForgePullInfo>._, A<DateTimeOffset>._, A<CancellationToken>._)).Returns(pull);
    }

    return pull;
  }

  private void Compared(GitSha baseSha, GitSha headSha, GitSha mergeBase, int behindBy, params string[] files)
  {
    A.CallTo(() => _gateway.CompareAsync(A<ForgeRepositoryRef>._, baseSha, headSha, A<CancellationToken>._))
     .Returns(new ForgeCompareResult(mergeBase, 1, behindBy, [.. files.Select(Changed)], Truncated: false));
  }

  private void PullFiles(int number, params string[] files)
  {
    A.CallTo(() => _gateway.ListPullFilesAsync(A<ForgeRepositoryRef>._, number, A<CancellationToken>._))
     .Returns(new ForgePullFiles([.. files.Select(Changed)], Truncated: false));
  }

  private static ForgeChangedFile Changed(string path) => new(path, null, ForgeChangedFileStatus.Modified, "@@ -1 +1 @@\n-a\n+b");

  private void Git(GitSha sha, params GitSha[] parents)
  {
    _git[sha] = GitCommit.Create(RepositoryId, new ForgeCommitInfo(sha, parents, "message", null, Now), Now);
  }

  private Commit Reported(GitSha sha) => Stored(sha, CommitState.Complete);

  private Commit Pending(GitSha sha) => Stored(sha, CommitState.Pending);

  private Commit Stored(GitSha sha, CommitState state)
  {
    var commit = Commit.Create(RepositoryId, sha, Now.AddDays(-1));

    if (state is not CommitState.Known)
    {
      commit.MarkPending("main", null, Now.AddDays(-1));
    }

    if (state is CommitState.Complete or CommitState.Error)
    {
      commit.RecordFinalization(state, 1, default, 1, 1, CarryforwardStatus.None, "source", Now.AddDays(-1));
    }
    else if (state is CommitState.Compacted)
    {
      typeof(Commit).GetProperty(nameof(Commit.State))!.SetValue(commit, state);
    }

    A.CallTo(() => _commits.FindByShaAsync(A<long>._, sha, A<CancellationToken>._)).Returns(commit);

    return commit;
  }

  #endregion
}