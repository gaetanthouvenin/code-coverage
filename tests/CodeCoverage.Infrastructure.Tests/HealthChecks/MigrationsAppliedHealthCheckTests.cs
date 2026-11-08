// ------------------------------------------------------------------------------------------------
// <copyright file="MigrationsAppliedHealthCheckTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using CodeCoverage.Infrastructure.HealthChecks;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.HealthChecks;

[Collection(DatabaseCollection.Name)]
public sealed class MigrationsAppliedHealthCheckTests(DatabaseFixture database)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The Web against a database the Api has not migrated yet: not ready.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Check_BeforeMigration_ShouldBeUnhealthy(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);

    var result = await CheckAsync(context);

    result.Status.ShouldBe(HealthStatus.Unhealthy);
    result.Description.ShouldNotBeNull().ShouldContain("not applied");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Check_AfterMigration_ShouldBeHealthy(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);
    await using var context = DatabaseFixture.CreateContext(provider, connectionString);
    await context.Database.MigrateAsync(Ct);

    var result = await CheckAsync(context);

    result.Status.ShouldBe(HealthStatus.Healthy);
  }

  private static Task<HealthCheckResult> CheckAsync(CodeCoverageDbContext context)
  {
    var check = new MigrationsAppliedHealthCheck(context);
    var registration = new HealthCheckRegistration(MigrationsAppliedHealthCheck.Name, check, HealthStatus.Unhealthy, null);

    return check.CheckHealthAsync(new HealthCheckContext { Registration = registration }, Ct);
  }

  #endregion
}
