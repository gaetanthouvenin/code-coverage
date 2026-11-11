// ------------------------------------------------------------------------------------------------
// <copyright file="NotificationGateTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;

namespace CodeCoverage.Domain.Tests.Notifications;

public sealed class NotificationGateTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private static readonly NotifyRule Default = new(AfterNBuilds: 0, WaitForCi: false, ManualTrigger: false, NotifyError: false);

  #endregion

  #region Methods

  [Fact]
  public void Decide_CompleteCommitWithoutPendingUpload_ShouldNotify()
  {
    Decide(Finalized(1, 1), 0, Default, true, false).ShouldBe(NotificationDecision.Notify);
  }

  // Every upload failed: the commit is in error, and that is what its statuses must say.
  [Fact]
  public void Decide_CommitInError_ShouldNotify()
  {
    Decide(Finalized(0, 0, CommitState.Error), 0, Default, true, false).ShouldBe(NotificationDecision.Notify);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void Decide_UploadsProcessing_ShouldWait(bool forced)
  {
    // Even forced: a notification never describes a part of what was sent.
    Decide(Finalized(1, 1), 1, Default, true, forced).ShouldBe(NotificationDecision.UploadsInProgress);
  }

  // The commit was finalized with one upload, and another one was processed since: the finalization it queued has not
  // run, so the report is the one of a part of what was sent. Nothing is notified from it, forced or not.
  [Theory]
  [InlineData(false, 2)]
  [InlineData(true, 2)]
  [InlineData(false, 3)]
  [InlineData(true, 3)]
  // An upload processed again and refused this time: the report still holds it.
  [InlineData(false, 0)]
  [InlineData(true, 0)]
  public void Decide_UploadsProcessedSinceTheFinalization_ShouldWaitForTheNextOne(bool forced, int processed)
  {
    NotificationGate.Decide(Finalized(1, 1), 0, processed, Default, true, forced).ShouldBe(NotificationDecision.NotFinalized);
  }

  // The uploads still in progress are named first: they are what the commit waits for.
  [Fact]
  public void Decide_UploadsProcessedSinceTheFinalizationAndOthersInProgress_ShouldNameTheUploadsInProgress()
  {
    NotificationGate.Decide(Finalized(1, 1), 1, 2, Default, true, true).ShouldBe(NotificationDecision.UploadsInProgress);
  }

  // The carried-forward sessions are not uploads: only the uploaded ones are compared.
  [Fact]
  public void Decide_CarriedForwardSessions_ShouldNotCountAsProcessedUploads()
  {
    NotificationGate.Decide(Finalized(sessions: 3, uploaded: 1), 0, 1, Default, true, false).ShouldBe(NotificationDecision.Notify);
    NotificationGate.Decide(Finalized(sessions: 3, uploaded: 1), 0, 3, Default, true, false).ShouldBe(NotificationDecision.NotFinalized);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void Decide_CommitNotFinalized_ShouldWait(bool pending)
  {
    var commit = Commit.Create(1, GitSha.Parse(new string('a', GitSha.Length)), Now);

    if (pending)
    {
      commit.MarkPending("main", null, Now);
    }

    Decide(commit, 0, Default, true, true).ShouldBe(NotificationDecision.NotFinalized);
  }

  [Fact]
  public void Decide_ManualTrigger_ShouldWaitUnlessForced()
  {
    var rule = Default with { ManualTrigger = true };

    Decide(Finalized(1, 1), 0, rule, true, false).ShouldBe(NotificationDecision.ManualTriggerExpected);
    Decide(Finalized(1, 1), 0, rule, true, true).ShouldBe(NotificationDecision.Notify);
  }

  [Fact]
  public void Decide_AfterNBuilds_ShouldCountCarriedForwardSessions()
  {
    // One uploaded session and two carried forward: three sessions, as len(report.sessions) of Codecov.
    var rule = Default with { AfterNBuilds = 3 };

    Decide(Finalized(sessions: 3, uploaded: 1), 0, rule, countCarriedForward: true, forced: false).ShouldBe(NotificationDecision.Notify);
    Decide(Finalized(sessions: 3, uploaded: 1), 0, rule, countCarriedForward: false, forced: false).ShouldBe(NotificationDecision.BuildsExpected);
  }

  [Theory]
  [InlineData(1, NotificationDecision.BuildsExpected)]
  [InlineData(2, NotificationDecision.Notify)]
  [InlineData(3, NotificationDecision.Notify)]
  public void Decide_AfterNBuilds_ShouldNotifyOnceReached(int sessions, NotificationDecision expected)
  {
    Decide(Finalized(sessions, sessions), 0, Default with { AfterNBuilds = 2 }, true, false).ShouldBe(expected);
  }

  [Fact]
  public void Decide_AfterNBuildsForced_ShouldNotify()
  {
    Decide(Finalized(1, 1), 0, Default with { AfterNBuilds = 5 }, true, true).ShouldBe(NotificationDecision.Notify);
  }

  [Fact]
  public void Decide_InvalidArgument_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => Decide(null!, 0, Default, true, false));
    Should.Throw<ArgumentNullException>(() => Decide(Finalized(1, 1), 0, null!, true, false));
    Should.Throw<ArgumentOutOfRangeException>(() => Decide(Finalized(1, 1), -1, Default, true, false));
    Should.Throw<ArgumentOutOfRangeException>(() => NotificationGate.Decide(Finalized(1, 1), 0, -1, Default, true, false));
  }

  [Fact]
  public void Create_ShouldDebounceUnderTheLockOfTheCommit()
  {
    var job = NotifyCommitJobs.Create(9, Now, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(300));

    job.Type.ShouldBe(JobTypes.NotifyCommit);
    job.PayloadJson.ShouldBe("""{"CommitId":9,"Force":false}""");
    job.LockKey.ShouldBe("commit:9");
    job.DedupKey.ShouldBe("notify:9");
    job.RunAfter.ShouldBe(Now.AddSeconds(20));
    job.MaxDelay.ShouldBe(TimeSpan.FromSeconds(300));
    job.Coalesce.ShouldBeFalse();
    job.IsInstance.ShouldBeFalse();
  }

  [Fact]
  public void Create_InvalidDelay_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => NotifyCommitJobs.Create(9, Now, TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(300)));
    Should.Throw<ArgumentOutOfRangeException>(() => NotifyCommitJobs.Create(9, Now, TimeSpan.Zero, TimeSpan.Zero));
  }

  // Merged with the debounced job, which keeps its payload, a forced request would no longer be forced.
  [Fact]
  public void CreateForced_ShouldRunAtOnceUnderItsOwnDedupKey()
  {
    var job = NotifyCommitJobs.CreateForced(9);

    job.PayloadJson.ShouldBe("""{"CommitId":9,"Force":true}""");
    job.LockKey.ShouldBe("commit:9");
    job.DedupKey.ShouldBe("notify:9:forced");
    job.DedupKey.ShouldNotBe(NotifyCommitJobs.Create(9, Now, TimeSpan.Zero, TimeSpan.FromSeconds(1)).DedupKey);
    job.RunAfter.ShouldBeNull();
    job.MaxDelay.ShouldBeNull();
    job.Coalesce.ShouldBeTrue();
  }

  // The usual case: the report of the commit holds every upload that was processed.
  private static NotificationDecision Decide(Commit commit, int uploadsInProgress, NotifyRule rule, bool countCarriedForward, bool forced)
  {
    return NotificationGate.Decide(commit, uploadsInProgress, commit?.UploadedSessionCount ?? 0, rule, countCarriedForward, forced);
  }

  [Theory]
  // sessions, uploaded, after_n_builds, carried forward counted, expected
  [InlineData(1, 1, 3, true, " (partial: 1/3 uploads)")]
  [InlineData(2, 1, 3, true, " (partial: 2/3 uploads)")]
  [InlineData(2, 1, 3, false, " (partial: 1/3 uploads)")]
  [InlineData(3, 3, 3, true, null)]
  [InlineData(4, 1, 3, true, null)]
  [InlineData(1, 1, 0, true, null)]
  public void PartialSuffix_ShouldCountTheSessionsAsTheGateDoes(int sessions, int uploaded, int afterNBuilds, bool countCarriedForward, string? expected)
  {
    var rule = new NotifyRule(afterNBuilds, false, false, false);

    NotificationGate.PartialSuffix(Finalized(sessions, uploaded), rule, countCarriedForward).ShouldBe(expected);
  }

  [Fact]
  public void PartialSuffix_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => NotificationGate.PartialSuffix(null!, new NotifyRule(1, false, false, false), true));
    Should.Throw<ArgumentNullException>(() => NotificationGate.PartialSuffix(Finalized(1, 1), null!, true));
  }

  private static Commit Finalized(int sessions, int uploaded, CommitState state = CommitState.Complete)
  {
    var commit = Commit.Create(1, GitSha.Parse(new string('a', GitSha.Length)), Now);

    commit.MarkPending("main", null, Now);
    commit.RecordFinalization(state, 1, default, sessions, uploaded, CarryforwardStatus.None, "source", Now);

    return commit;
  }

  #endregion
}