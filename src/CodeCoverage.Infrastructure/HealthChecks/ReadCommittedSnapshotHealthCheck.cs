// ------------------------------------------------------------------------------------------------
// <copyright file="ReadCommittedSnapshotHealthCheck.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.HealthChecks;

/// <summary>Reports whether <c>READ_COMMITTED_SNAPSHOT</c> is on, under SQL Server.</summary>
/// <param name="dbContext">The context of the database to check.</param>
/// <remarks>
///   <para>
///     Under SQL Server, the readers must not block the writers (job queue, uploads): row versioning
///     (<c>ALTER DATABASE … SET READ_COMMITTED_SNAPSHOT ON</c>) is required. Azure SQL has it on by
///     default; an on-premises database has it off until an administrator turns it on.
///   </para>
///   <para>PostgreSQL never blocks readers on writers: the check is healthy there.</para>
/// </remarks>
internal sealed class ReadCommittedSnapshotHealthCheck(CodeCoverageDbContext dbContext) : IHealthCheck
{
  #region Fields

  /// <summary>The name of the check in the health report.</summary>
  public const string Name = "read-committed-snapshot";

  #endregion

  #region Implementation of IHealthCheck

  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(context);

    if (!dbContext.Database.IsSqlServer())
    {
      return HealthCheckResult.Healthy("Not applicable to this database engine.");
    }

    var enabled = await dbContext.Database
                                 .SqlQuery<bool>(
                                   $"SELECT is_read_committed_snapshot_on AS [Value] FROM sys.databases WHERE name = DB_NAME()"
                                 )
                                 .SingleAsync(cancellationToken)
                                 .ConfigureAwait(false);

    return enabled
             ? HealthCheckResult.Healthy("READ_COMMITTED_SNAPSHOT is on.")
             : new HealthCheckResult(
               context.Registration.FailureStatus,
               "READ_COMMITTED_SNAPSHOT is off: run ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON."
             );
  }

  #endregion
}
