// ------------------------------------------------------------------------------------------------
// <copyright file="ConnectionHealthJobsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Domain.Tests.Contracts.Jobs;

public sealed class ConnectionHealthJobsTests
{
  #region Methods

  [Fact]
  public void CheckPayload_ShouldNameTheConnection()
  {
    ConnectionHealthJobs.CheckPayload("contoso-devops").ShouldBe("""{"ConnectionId":"contoso-devops"}""");
  }

  [Fact]
  public void CheckKey_ShouldStayApartFromTheLockOfTheConnection()
  {
    ConnectionHealthJobs.CheckKey("contoso-devops").ShouldBe("check:connection:contoso-devops");
    ConnectionHealthJobs.CheckKey("contoso-devops").ShouldNotBe(RepositoryReconciliationJobs.ConnectionKey("contoso-devops"));
  }

  [Fact]
  public void CheckPayload_NullConnection_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => ConnectionHealthJobs.CheckPayload(null!));
  }

  [Theory]
  [InlineData("")]
  [InlineData("Contoso")]
  [InlineData("-contoso")]
  public void CheckKey_MalformedConnection_ShouldThrowArgumentException(string connectionId)
  {
    Should.Throw<ArgumentException>(() => ConnectionHealthJobs.CheckKey(connectionId)).ParamName.ShouldBe("connectionId");
  }

  #endregion
}
