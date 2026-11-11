// ------------------------------------------------------------------------------------------------
// <copyright file="SyncPullRequestJobsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Domain.Tests.Contracts.Jobs;

public sealed class SyncPullRequestJobsTests
{
  #region Methods

  [Fact]
  public void Create_ShouldLockAndMergeByPullRequestInTheOrganizationOfTheScope()
  {
    var job = SyncPullRequestJobs.Create(5, 12);

    job.Type.ShouldBe(JobTypes.SyncPullRequest);
    job.IsInstance.ShouldBeFalse();
    job.OrganizationId.ShouldBeNull();
    job.LockKey.ShouldBe("pull:5:12");
    job.DedupKey.ShouldBe("syncpr:5:12");
    job.Coalesce.ShouldBeTrue();
    job.PayloadJson.ShouldBe("""{"RepositoryId":5,"Number":12}""");
  }

  [Fact]
  public void Create_OtherPullRequestOrRepository_ShouldNotShareAKey()
  {
    SyncPullRequestJobs.Create(5, 13).DedupKey.ShouldNotBe(SyncPullRequestJobs.Create(5, 12).DedupKey);
    SyncPullRequestJobs.Create(51, 2).LockKey.ShouldNotBe(SyncPullRequestJobs.Create(5, 12).LockKey);
  }

  // The notification of the sweep keeps its own deduplication key: merged with a notification asked by hand, which
  // keeps its payload, one of the two would lose its mode.
  [Fact]
  public void CreateForSweep_ShouldBeForcedPartialAndApartFromTheOtherNotifications()
  {
    var job = NotifyCommitJobs.CreateForSweep(9);

    job.Type.ShouldBe(JobTypes.NotifyCommit);
    job.PayloadJson.ShouldBe("""{"CommitId":9,"Force":true,"Partial":true}""");
    job.LockKey.ShouldBe("commit:9");
    job.DedupKey.ShouldBe("notify:9:sweep");
    job.Coalesce.ShouldBeTrue();
    job.OrganizationId.ShouldBeNull();
    job.IsInstance.ShouldBeFalse();
    job.DedupKey.ShouldNotBe(NotifyCommitJobs.CreateForced(9).DedupKey);
    job.LockKey.ShouldBe(NotifyCommitJobs.CreateForced(9).LockKey);
  }

  [Theory]
  [InlineData(0, 1)]
  [InlineData(1, 0)]
  [InlineData(-1, 1)]
  public void Create_InvalidArgument_ShouldThrow(long repositoryId, int number)
  {
    Should.Throw<ArgumentOutOfRangeException>(() => SyncPullRequestJobs.Create(repositoryId, number));
  }

  #endregion
}
