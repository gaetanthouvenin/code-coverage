// ------------------------------------------------------------------------------------------------
// <copyright file="DataProtectionWiringTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Smoke;

[Collection(CodeCoverageWebCollection.Name)]
public sealed class DataProtectionWiringTests(CodeCoverageWebFactory factory)
{
  #region Methods

  [Fact]
  public void Services_ShouldProtectAndUnprotectSecrets()
  {
    var protector = factory.Services.GetRequiredService<ISecretProtector>();

    protector.Unprotect(protector.Protect("s3cr3t")).ShouldBe("s3cr3t");
  }

  [Fact]
  public async Task Start_InProductionWithoutKeyProtection_ShouldRefuseToStart()
  {
    await using var production = factory.WithWebHostBuilder(
      b => b.UseEnvironment("Production")
            .UseSetting("DataProtection:AllowUnprotectedKeys", "false"));

    Should.Throw<InvalidOperationException>(() => production.CreateClient())
          .Message.ShouldContain("DataProtection__AllowUnprotectedKeys");
  }

  #endregion
}
