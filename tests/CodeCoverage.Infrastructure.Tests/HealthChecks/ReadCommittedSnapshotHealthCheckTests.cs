// ------------------------------------------------------------------------------------------------
// <copyright file="ReadCommittedSnapshotHealthCheckTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using CodeCoverage.Infrastructure.HealthChecks;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.HealthChecks;

[Collection(DatabaseCollection.Name)]
public sealed class ReadCommittedSnapshotHealthCheckTests(DatabaseFixture database)
{
  #region Fields

  // Never opened: the check does not reach a PostgreSQL server.
  private const string UnusedConnection = "Host=localhost;Database=unused";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // EF Core turns READ_COMMITTED_SNAPSHOT on when it creates the database; a database created by an
  // administrator has it off, which the test reproduces.
  [Fact]
  public async Task Check_WithRcsiOff_ShouldBeUnhealthy()
  {
    await using var context = await CreateSqlServerDatabaseAsync();
    await context.Database.ExecuteSqlRawAsync("ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE", Ct);

    // ROLLBACK IMMEDIATE kills the other sessions on the database: drop their pooled connections.
    SqlConnection.ClearAllPools();

    var result = await CheckAsync(context);

    result.Status.ShouldBe(HealthStatus.Unhealthy);
    result.Description.ShouldNotBeNull().ShouldContain("READ_COMMITTED_SNAPSHOT is off");
  }

  [Fact]
  public async Task Check_WithRcsiOn_ShouldBeHealthy()
  {
    await using var context = await CreateSqlServerDatabaseAsync();
    await context.Database.ExecuteSqlRawAsync("ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE", Ct);

    // ROLLBACK IMMEDIATE kills the other sessions on the database: drop their pooled connections.
    SqlConnection.ClearAllPools();

    var result = await CheckAsync(context);

    result.Status.ShouldBe(HealthStatus.Healthy);
  }

  [Fact]
  public async Task Check_OnPostgreSql_ShouldBeHealthyWithoutQuerying()
  {
    await using var context = DatabaseFixture.CreateContext(DatabaseProvider.PostgreSql, UnusedConnection);

    var result = await CheckAsync(context);

    result.Status.ShouldBe(HealthStatus.Healthy);
  }

  private static Task<HealthCheckResult> CheckAsync(CodeCoverageDbContext context)
  {
    var check = new ReadCommittedSnapshotHealthCheck(context);
    var registration = new HealthCheckRegistration(ReadCommittedSnapshotHealthCheck.Name, check, HealthStatus.Unhealthy, null);

    return check.CheckHealthAsync(new HealthCheckContext { Registration = registration }, Ct);
  }

  private async Task<CodeCoverageDbContext> CreateSqlServerDatabaseAsync()
  {
    var connectionString = await database.NewDatabaseAsync(DatabaseProvider.SqlServer, Ct);
    var context = DatabaseFixture.CreateContext(DatabaseProvider.SqlServer, connectionString);

    await context.Database.MigrateAsync(Ct);

    return context;
  }

  #endregion
}
