// ------------------------------------------------------------------------------------------------
// <copyright file="JobClaimDialectTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using Npgsql;

using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.PostgreSql.Jobs;
using CodeCoverage.Infrastructure.SqlServer.Jobs;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

// The SQL of each engine, without a server: its behaviour is covered by JobClaimConcurrencyTests.
public sealed class JobClaimDialectTests
{
  #region Fields

  private const string Worker = "worker-1'; DROP TABLE \"Jobs\"; --";

  private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  private static readonly DateTimeOffset LockedUntil = Now.AddMinutes(5);

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void ClaimNext_ShouldSendTheValuesAsParameters(DatabaseProvider provider)
  {
    var dialect = JobClaimDialects.For(provider);

    var statement = dialect.ClaimNext(Worker, Now, LockedUntil);

    dialect.Provider.ShouldBe(provider);
    statement.GetArguments().ShouldBe([Worker, LockedUntil, Now], ignoreOrder: true);
    statement.Format.ShouldNotContain("worker-1");
    statement.Format.ShouldNotContain("2026");
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void ClaimNext_ShouldClaimOnlyDueQueuedJobsWhoseKeyIsFree(DatabaseProvider provider)
  {
    var format = JobClaimDialects.For(provider).ClaimNext(Worker, Now, LockedUntil).Format;

    format.ShouldContain("'Queued'");
    format.ShouldContain("'Running'");
    format.ShouldContain("IS NULL");
    format.ShouldContain("NOT EXISTS");
    format.ShouldContain("+ 1");
  }

  [Fact]
  public void PostgreSql_ClaimNext_ShouldSkipLockedRowsAndReturnTheClaimedRow()
  {
    var format = new PostgreSqlJobClaimDialect().ClaimNext(Worker, Now, LockedUntil).Format;

    format.ShouldContain("FOR UPDATE SKIP LOCKED");
    format.ShouldContain("ORDER BY j.\"RunAfter\", j.\"Id\"");
    format.ShouldContain("LIMIT 1");
    format.ShouldEndWith("RETURNING *");
  }

  [Fact]
  public void SqlServer_ClaimNext_ShouldReadPastLockedRowsAndReturnTheClaimedRow()
  {
    var format = new SqlServerJobClaimDialect().ClaimNext(Worker, Now, LockedUntil).Format;

    format.ShouldContain("WITH (ROWLOCK, UPDLOCK, READPAST)");
    format.ShouldContain("SELECT TOP (1)");
    format.ShouldContain("ORDER BY j.[RunAfter], j.[Id]");
    format.ShouldEndWith("OUTPUT inserted.*");
  }

  [Fact]
  public void PostgreSql_IsDeadlock_ShouldRecognizeOnlyTheDeadlockError()
  {
    var dialect = new PostgreSqlJobClaimDialect();

    dialect.IsDeadlock(new PostgresException("deadlock", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected)).ShouldBeTrue();
    dialect.IsDeadlock(new PostgresException("duplicate", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation)).ShouldBeFalse();
  }

  [Fact]
  public void SqlServer_IsDeadlock_WithAnotherEngineError_ShouldReturnFalse()
  {
    new SqlServerJobClaimDialect()
      .IsDeadlock(new PostgresException("deadlock", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected))
      .ShouldBeFalse();
  }

  // The statement is an UPDATE: EF Core must run it as written, never wrap it in a SELECT.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task ClaimNext_ThroughFromSql_ShouldNotBeComposed(DatabaseProvider provider)
  {
    await using var context = DatabaseFixture.CreateContext(provider, "Server=localhost;Database=unused");

    var sql = context.Jobs
                     .FromSql(JobClaimDialects.For(provider).ClaimNext(Worker, Now, LockedUntil))
                     .AsNoTracking()
                     .ToQueryString();

    sql.ShouldNotContain("FROM (");
    sql.TrimStart().ShouldNotStartWith("SELECT");
  }

  #endregion
}
