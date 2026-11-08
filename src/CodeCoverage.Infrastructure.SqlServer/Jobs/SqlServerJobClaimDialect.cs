// ------------------------------------------------------------------------------------------------
// <copyright file="SqlServerJobClaimDialect.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

using Microsoft.Data.SqlClient;

using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.SqlServer.Jobs;

/// <summary>Claims the next job under SQL Server, with <c>WITH (ROWLOCK, UPDLOCK, READPAST)</c>.</summary>
/// <remarks>
///   <para>
///     The common table expression reads the queued jobs with update locks (a locking read, even under
///     <c>READ_COMMITTED_SNAPSHOT</c>) and skips the rows already locked by the other workers: two
///     workers never claim the same job, and none waits for another. The update goes through the
///     expression, so the chosen row is claimed in the same statement.
///   </para>
///   <para>
///     The <c>NOT EXISTS</c> clause skips the keys that already have a running job. Two workers may
///     still pass it at the same time for two jobs of one key: the unique index
///     <c>UX_Jobs_LockKey_Running</c> then refuses the second statement, and the queue absorbs the
///     refusal.
///   </para>
/// </remarks>
internal sealed class SqlServerJobClaimDialect : IJobClaimDialect
{
  #region Fields

  /// <summary>The SQL Server error of a statement chosen as the victim of a deadlock.</summary>
  private const int DeadlockVictim = 1205;

  #endregion

  #region Implementation of IJobClaimDialect

  /// <inheritdoc />
  public DatabaseProvider Provider => DatabaseProvider.SqlServer;

  /// <inheritdoc />
  public FormattableString ClaimNext(string lockedBy, DateTimeOffset now, DateTimeOffset lockedUntil) =>
    $"""
     WITH [Claimed] AS (
       SELECT TOP (1) j.*
       FROM [Jobs] AS j WITH (ROWLOCK, UPDLOCK, READPAST)
       WHERE j.[State] = N'Queued'
         AND j.[RunAfter] <= {now}
         AND (j.[LockKey] IS NULL
              OR NOT EXISTS (SELECT 1 FROM [Jobs] AS r WHERE r.[State] = N'Running' AND r.[LockKey] = j.[LockKey]))
       ORDER BY j.[RunAfter], j.[Id])
     UPDATE [Claimed]
     SET [State] = N'Running', [LockedBy] = {lockedBy}, [LockedUntil] = {lockedUntil}, [Attempts] = [Attempts] + 1
     OUTPUT inserted.*
     """;

  /// <inheritdoc />
  public bool IsDeadlock(DbException exception) => exception is SqlException { Number: DeadlockVictim };

  #endregion
}
