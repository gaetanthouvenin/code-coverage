// ------------------------------------------------------------------------------------------------
// <copyright file="InstallationJobsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Domain.Tests.Contracts.Jobs;

public sealed class InstallationJobsTests
{
  #region Methods

  [Fact]
  public void Create_ShouldRunOnTheLaneOfTheInstanceApartFromTheDailyJob()
  {
    var job = SyncInstallationsJobs.Create();

    job.IsInstance.ShouldBeTrue();
    job.Type.ShouldBe(JobTypes.SyncInstallations);
    job.LockKey.ShouldBe("installations");
    job.DedupKey.ShouldBe("installations:event");
    job.DedupKey.ShouldNotBe(SyncInstallationsJobs.Key);
    job.Coalesce.ShouldBeTrue();
    job.PayloadJson.ShouldBe("{}");
  }

  [Fact]
  public void CreateForTransfer_ShouldNameTheRepositoryAndTheOriginInItsOwnDedupKey()
  {
    var job = SyncInstallationsJobs.CreateForTransfer("7", "2002");

    job.IsInstance.ShouldBeTrue();
    job.LockKey.ShouldBe("installations");
    job.DedupKey.ShouldBe("installations:transfer:7:2002");
    job.PayloadJson.ShouldBe("""{"TransferredRepositoryId":"7","PreviousOwnerAccountId":"2002"}""");
  }

  // A merged job keeps its own payload: a second transfer, from another account, must stay a job of its own.
  [Fact]
  public void CreateForTransfer_SameRepositoryFromAnotherAccount_ShouldNotShareTheDedupKey()
  {
    SyncInstallationsJobs.CreateForTransfer("7", "3003").DedupKey.ShouldNotBe(SyncInstallationsJobs.CreateForTransfer("7", "2002").DedupKey);
    SyncInstallationsJobs.CreateForTransfer("7", "2002").DedupKey.ShouldBe(SyncInstallationsJobs.CreateForTransfer("7", "2002").DedupKey);
  }

  [Theory]
  [InlineData("", "2002")]
  [InlineData("7", " ")]
  public void CreateForTransfer_EmptyArgument_ShouldThrow(string repository, string owner)
  {
    Should.Throw<ArgumentException>(() => SyncInstallationsJobs.CreateForTransfer(repository, owner));
  }

  [Fact]
  public void ForInstallation_ShouldTakeTheOrganizationOfItsScopeAndTheLockOfTheInstallation()
  {
    var job = RepositoryReconciliationJobs.ForInstallation(42);

    job.IsInstance.ShouldBeFalse();
    job.OrganizationId.ShouldBeNull();
    job.Type.ShouldBe(JobTypes.SyncInstallationRepositories);
    job.LockKey.ShouldBe("installation:42");
    job.DedupKey.ShouldBe("sync:installation:42");
    job.Coalesce.ShouldBeTrue();
    job.PayloadJson.ShouldBe("""{"InstallationId":42}""");
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void ForInstallation_NotPositive_ShouldThrow(long installationId)
  {
    Should.Throw<ArgumentOutOfRangeException>(() => RepositoryReconciliationJobs.ForInstallation(installationId));
  }

  [Fact]
  public void TransferPayload_ShouldNameTheRepository()
  {
    RepositoryReconciliationJobs.TransferPayload("7").ShouldBe("""{"RepositoryExternalId":"7"}""");
    RepositoryReconciliationJobs.RepositoryKey("7").ShouldBe("repo:7");
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void TransferPayloadAndKey_EmptyRepository_ShouldThrow(string repository)
  {
    Should.Throw<ArgumentException>(() => RepositoryReconciliationJobs.TransferPayload(repository));
    Should.Throw<ArgumentException>(() => RepositoryReconciliationJobs.RepositoryKey(repository));
  }

  #endregion
}