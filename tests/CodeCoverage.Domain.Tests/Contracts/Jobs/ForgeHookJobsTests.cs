// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookJobsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Domain.Tests.Contracts.Jobs;

public sealed class ForgeHookJobsTests
{
  #region Methods

  [Fact]
  public void EnsurePayload_ShouldNameTheConnection()
  {
    ForgeHookJobs.EnsurePayload("contoso-devops").ShouldBe("""{"ConnectionId":"contoso-devops"}""");
  }

  [Fact]
  public void EnsureKey_ShouldStayApartFromTheOtherKeysOfTheConnection()
  {
    ForgeHookJobs.EnsureKey("contoso-devops").ShouldBe("hooks:connection:contoso-devops");
    ForgeHookJobs.EnsureKey("contoso-devops").ShouldNotBe(RepositoryReconciliationJobs.ConnectionKey("contoso-devops"));
    ForgeHookJobs.EnsureKey("contoso-devops").ShouldNotBe(ConnectionHealthJobs.CheckKey("contoso-devops"));
  }

  [Fact]
  public void Ensure_ShouldMergeTheJobOfTheConnectionInTheOrganizationOfTheScope()
  {
    var job = ForgeHookJobs.Ensure("contoso-devops");

    job.Type.ShouldBe(JobTypes.EnsureForgeHooks);
    job.PayloadJson.ShouldBe(ForgeHookJobs.EnsurePayload("contoso-devops"));
    job.LockKey.ShouldBe("hooks:connection:contoso-devops");
    job.DedupKey.ShouldBe("hooks:connection:contoso-devops");
    job.Coalesce.ShouldBeTrue();
    job.IsInstance.ShouldBeFalse();
    job.OrganizationId.ShouldBeNull();
  }

  [Fact]
  public void EnsurePayload_NullConnection_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => ForgeHookJobs.EnsurePayload(null!));
  }

  [Theory]
  [InlineData("")]
  [InlineData("Contoso")]
  [InlineData("-contoso")]
  public void EnsureKey_MalformedConnection_ShouldThrowArgumentException(string connectionId)
  {
    Should.Throw<ArgumentException>(() => ForgeHookJobs.EnsureKey(connectionId)).ParamName.ShouldBe("connectionId");
  }

  #endregion
}
