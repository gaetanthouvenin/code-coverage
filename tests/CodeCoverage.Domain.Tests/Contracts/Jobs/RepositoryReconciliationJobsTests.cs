// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryReconciliationJobsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Contracts.Jobs;

public sealed class RepositoryReconciliationJobsTests
{
  #region Methods

  [Fact]
  public void ForConnection_Id_ShouldPrefixTheKey()
  {
    var job = RepositoryReconciliationJobs.ForConnection(Forge.AzureDevOps, "contoso-devops");

    job.IsInstance.ShouldBeFalse();
    job.OrganizationId.ShouldBeNull();
    job.Type.ShouldBe(JobTypes.SyncConnectionRepositories);
    job.LockKey.ShouldBe("connection:contoso-devops");
    job.DedupKey.ShouldBe("sync:connection:contoso-devops");
    job.Coalesce.ShouldBeTrue();
    job.PayloadJson.ShouldBe("""{"ConnectionId":"contoso-devops","Forge":"AzureDevOps"}""");
  }

  // A connection whose identifier holds digits alone is no installation: it keeps a key of its own.
  [Fact]
  public void ForConnection_NumericId_ShouldNotShareTheKeyOfAnInstallation()
  {
    RepositoryReconciliationJobs.ForConnection(Forge.GitLab, "42").LockKey.ShouldNotBe(RepositoryReconciliationJobs.ForInstallation(42).LockKey);
  }

  [Theory]
  [InlineData("")]
  [InlineData("Contoso")]
  [InlineData("-contoso")]
  [InlineData("contoso devops")]
  public void ForConnection_NotWellFormed_ShouldThrow(string connectionId)
  {
    Should.Throw<ArgumentException>(() => RepositoryReconciliationJobs.ForConnection(Forge.Bitbucket, connectionId));
  }

  [Fact]
  public void ForConnection_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => RepositoryReconciliationJobs.ForConnection(Forge.Bitbucket, null!));
  }

  // GitHub keeps the key of its installation, the lock its deliveries take.
  [Fact]
  public void ForConnection_GitHub_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => RepositoryReconciliationJobs.ForConnection(Forge.GitHub, "42"));
  }

  [Fact]
  public void ForInstallation_ShouldRunTheSameTypeAsAConnectionUnderTheLockOfTheInstallation()
  {
    var job = RepositoryReconciliationJobs.ForInstallation(42);

    job.Type.ShouldBe(RepositoryReconciliationJobs.ForConnection(Forge.AzureDevOps, "contoso-devops").Type);
    job.LockKey.ShouldBe("installation:42");
    job.PayloadJson.ShouldBe(RepositoryReconciliationJobs.ConnectionPayload(Forge.GitHub, "42"));
  }

  #endregion
}
