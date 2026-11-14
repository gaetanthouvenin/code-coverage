// ------------------------------------------------------------------------------------------------
// <copyright file="BranchHeadTrackerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Services;

public sealed class BranchHeadTrackerTests
{
  #region Fields

  private const long RepositoryId = 5;

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private static readonly ForgeRepositoryRef Address = new(Forge.GitHub, "42", "7");

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IGitCommitRepository _git = A.Fake<IGitCommitRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly Dictionary<GitSha, Commit> _stored = [];

  private readonly BranchHeadTracker _tracker;

  private long _nextId = 100;

  #endregion

  #region Ctors

  public BranchHeadTrackerTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    var clock = A.Fake<IClock>();

    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _git.FindAsync(RepositoryId, A<GitSha>._, A<CancellationToken>._)).Returns((GitCommit?)null);
    A.CallTo(() => _gateway.ListCommitsAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<int>._, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => _commits.ListByShasAsync(RepositoryId, A<IReadOnlyCollection<GitSha>>._, A<CancellationToken>._))
     .ReturnsLazily((long _, IReadOnlyCollection<GitSha> shas, CancellationToken _) =>
                      Task.FromResult<IReadOnlyList<Commit>>([.. shas.Where(_stored.ContainsKey).Select(sha => _stored[sha])]));

    _tracker = new BranchHeadTracker(_commits, new CommitAncestry(_git, gateways, clock));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Track_CompleteHead_ShouldPickTheHead()
  {
    History(1, 2, 3);
    var head = Stored(1, CommitState.Complete);
    Stored(2, CommitState.Complete);

    (await TrackAsync(1)).ShouldBe(head.Id);
  }

  [Fact]
  public async Task Track_ShouldPickNearestCompleteAncestor()
  {
    History(1, 2, 3, 4);
    Stored(1, CommitState.Pending);
    Stored(2, CommitState.Error);
    var nearest = Stored(3, CommitState.Complete);
    Stored(4, CommitState.Complete);

    (await TrackAsync(1)).ShouldBe(nearest.Id);
  }

  [Fact]
  public async Task Track_AfterForcePush_ShouldDropOldHead()
  {
    // Before the forced push the branch was 9 -> 3 -> 4; it is now 1 -> 3 -> 4. The former head is complete and more
    // recent than anything else, but it is no longer an ancestor: no date is compared.
    History(1, 3, 4);
    History(9, 3);
    Stored(9, CommitState.Complete);
    var shared = Stored(3, CommitState.Complete);

    (await TrackAsync(9)).ShouldNotBe(shared.Id);
    (await TrackAsync(1)).ShouldBe(shared.Id);
  }

  // An imported commit has no report to show: it never heads a branch.
  [Fact]
  public async Task Track_ImportedAncestor_ShouldBeSkipped()
  {
    History(1, 2, 3);
    var imported = Commit.Import(RepositoryId, Sha(2), "main", 1, new CoverageTotals(1, 0, 0, 0, 0), Now, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(imported, ++_nextId);
    _stored[imported.Sha] = imported;
    var real = Stored(3, CommitState.Complete);

    (await TrackAsync(1)).ShouldBe(real.Id);
  }

  [Fact]
  public async Task Track_NoCompleteCommit_ShouldFindNone()
  {
    History(1, 2);
    Stored(1, CommitState.Known);
    Stored(2, CommitState.Compacted);

    (await TrackAsync(1)).ShouldBeNull();
  }

  [Fact]
  public async Task Track_HeadUnknownToTheForge_ShouldOnlyLookAtTheHead()
  {
    var head = Stored(1, CommitState.Complete);

    (await TrackAsync(1)).ShouldBe(head.Id);
  }

  [Fact]
  public async Task Track_ShouldFollowTheFirstParentOnly()
  {
    // 1 merges 3 into 2: the branch itself went 1 -> 2, and 3 belongs to the branch that was merged.
    Git(1, 2, 3);
    Git(2);
    Git(3);
    Stored(3, CommitState.Complete);

    (await TrackAsync(1)).ShouldBeNull();
  }

  [Fact]
  public async Task Track_ShouldStopAfterFiftyAncestors()
  {
    History([.. Enumerable.Range(1, 53)]);
    Stored(52, CommitState.Complete);

    (await TrackAsync(1)).ShouldBeNull();

    var within = Stored(51, CommitState.Complete);

    (await TrackAsync(1)).ShouldBe(within.Id);
  }

  [Fact]
  public async Task Track_CommitBeingFinalizedAsComplete_ShouldPickItAlthoughItsRowIsNotCompleteYet()
  {
    History(1, 2);
    var finalizing = Stored(1, CommitState.Pending);
    Stored(2, CommitState.Complete);

    (await _tracker.TrackAsync(RepositoryId, Address, Sha(1), finalizing, CommitState.Complete, Ct)).ShouldBe(finalizing.Id);
  }

  [Fact]
  public async Task Track_CommitBeingFinalizedAsError_ShouldPassItAlthoughItsRowIsStillComplete()
  {
    History(1, 2);
    var finalizing = Stored(1, CommitState.Complete);
    var ancestor = Stored(2, CommitState.Complete);

    (await _tracker.TrackAsync(RepositoryId, Address, Sha(1), finalizing, CommitState.Error, Ct)).ShouldBe(ancestor.Id);
  }

  [Fact]
  public async Task Track_CommitBeingFinalizedBehindTheHead_ShouldBePickedWhenItIsTheNearest()
  {
    History(1, 2, 3);
    Stored(1, CommitState.Known);
    var finalizing = Stored(2, CommitState.Pending);
    Stored(3, CommitState.Complete);

    (await _tracker.TrackAsync(RepositoryId, Address, Sha(1), finalizing, CommitState.Complete, Ct)).ShouldBe(finalizing.Id);
  }

  [Fact]
  public async Task Track_NullArguments_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => _tracker.TrackAsync(RepositoryId, null!, Sha(1), null, null, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _tracker.TrackAsync(RepositoryId, Address, null!, null, null, Ct));
  }

  private static GitSha Sha(int number) => GitSha.Parse(number.ToString("x40", System.Globalization.CultureInfo.InvariantCulture));

  private Task<long?> TrackAsync(int head) => _tracker.TrackAsync(RepositoryId, Address, Sha(head), null, null, Ct);

  // A linear history: each commit has the next one as its only parent, and the last one has none.
  private void History(params int[] commits)
  {
    for (var index = 0; index < commits.Length; index++)
    {
      if (index + 1 < commits.Length)
      {
        Git(commits[index], commits[index + 1]);
      }
      else
      {
        Git(commits[index]);
      }
    }
  }

  private void Git(int commit, params int[] parents)
  {
    var sha = Sha(commit);

    A.CallTo(() => _git.FindAsync(RepositoryId, sha, A<CancellationToken>._))
     .Returns(GitCommit.Create(RepositoryId, new ForgeCommitInfo(sha, [.. parents.Select(Sha)], "message", null, Now), Now));
  }

  private Commit Stored(int number, CommitState state)
  {
    var commit = Commit.Create(RepositoryId, Sha(number), Now);

    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, ++_nextId);
    typeof(Commit).GetProperty(nameof(Commit.State))!.SetValue(commit, state);
    _stored[commit.Sha] = commit;

    return commit;
  }

  #endregion
}
