// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeQuotaTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Operations;

public sealed class ForgeQuotaTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.FromHours(1));

  #endregion

  #region Methods

  [Fact]
  public void Create_ShouldKeepTheForgeTheConnectionAndUtcTimes()
  {
    var quota = ForgeQuota.Create(Forge.AzureDevOps, "connection-1", 180, 200, Now.AddMinutes(5), Now);

    (quota.Forge, quota.Connection, quota.Remaining, quota.Limit).ShouldBe((Forge.AzureDevOps, "connection-1", 180L, (long?)200));
    quota.ResetAt.ShouldNotBeNull().Offset.ShouldBe(TimeSpan.Zero);
    quota.ResetAt.ShouldBe(Now.AddMinutes(5));
    quota.ObservedAt.Offset.ShouldBe(TimeSpan.Zero);
    quota.ObservedAt.ShouldBe(Now);
  }

  [Fact]
  public void Create_WithoutLimitNorReset_ShouldKeepThemUnknown()
  {
    var quota = ForgeQuota.Create(Forge.GitHub, ForgeQuota.AppConnection, 4999, null, null, Now);

    (quota.Limit, quota.ResetAt).ShouldBe(((long?)null, (DateTimeOffset?)null));
  }

  [Fact]
  public void Create_LongestConnection_ShouldBeAccepted()
  {
    var connection = new string('c', ForgeQuota.ConnectionMaxLength);

    ForgeQuota.Create(Forge.GitLab, connection, 1, null, null, Now).Connection.ShouldBe(connection);
  }

  [Fact]
  public void Create_InvalidConnection_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => ForgeQuota.Create(Forge.GitHub, " ", 1, null, null, Now));
    Should.Throw<ArgumentNullException>(() => ForgeQuota.Create(Forge.GitHub, null!, 1, null, null, Now));
    Should.Throw<ArgumentOutOfRangeException>(() => ForgeQuota.Create(Forge.GitHub, new string('c', ForgeQuota.ConnectionMaxLength + 1), 1, null, null, Now));
  }

  #endregion
}
