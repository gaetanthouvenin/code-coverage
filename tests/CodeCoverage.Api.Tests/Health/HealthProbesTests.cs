// ------------------------------------------------------------------------------------------------
// <copyright file="HealthProbesTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeCoverage.Api.Configuration;
using CodeCoverage.Api.Tests.Support;

namespace CodeCoverage.Api.Tests.Health;

[Collection(CodeCoverageApiCollection.Name)]
public sealed class HealthProbesTests(CodeCoverageApiFactory factory)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_Live_ShouldReturn200Healthy()
  {
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(HealthProbes.LivePath, Ct);
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    document.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");
  }

  [Fact]
  public async Task Get_Ready_WithUnreachableDatabase_ShouldReturn503WithTheDatabaseChecks()
  {
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(HealthProbes.ReadyPath, Ct);
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    document.RootElement.GetProperty("status").GetString().ShouldBe("Unhealthy");

    var checks = document.RootElement.GetProperty("checks");
    checks.GetProperty("database").GetString().ShouldBe("Unhealthy");
    checks.GetProperty("migrations").GetString().ShouldBe("Unhealthy");
    checks.GetProperty("read-committed-snapshot").GetString().ShouldBe("Healthy");
  }

  // Only the Api migrates at start-up.
  [Fact]
  public void Host_ShouldRegisterTheDatabaseMigrator()
  {
    var hostedServices = factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Name);

    hostedServices.ShouldContain("DatabaseMigrator");
  }

  #endregion
}
