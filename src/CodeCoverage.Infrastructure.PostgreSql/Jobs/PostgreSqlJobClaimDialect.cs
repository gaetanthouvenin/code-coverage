// ------------------------------------------------------------------------------------------------
// <copyright file="PostgreSqlJobClaimDialect.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

using Npgsql;

using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.PostgreSql.Jobs;

/// <summary>Claims the next job under PostgreSQL, with <c>FOR UPDATE SKIP LOCKED</c>.</summary>
/// <remarks>
///   <para>
///     The subquery locks the row of the chosen job and skips the rows already locked by the other
///     workers: two workers never claim the same job, and none waits for another. A row claimed by a
///     concurrent statement in the meantime is checked again once it is unlocked, no longer matches
///     <c>State = 'Queued'</c>, and the next row is taken instead.
///   </para>
///   <para>
///     The <c>NOT EXISTS</c> clause skips the keys that already have a running job. Two workers may
///     still pass it at the same time for two jobs of one key: the unique index
///     <c>UX_Jobs_LockKey_Running</c> then refuses the second statement, and the queue absorbs the
///     refusal.
///   </para>
/// </remarks>
internal sealed class PostgreSqlJobClaimDialect : IJobClaimDialect
{
  #region Implementation of IJobClaimDialect

  /// <inheritdoc />
  public DatabaseProvider Provider => DatabaseProvider.PostgreSql;

  /// <inheritdoc />
  public FormattableString ClaimNext(string lockedBy, DateTimeOffset now, DateTimeOffset lockedUntil) =>
    $"""
     UPDATE "Jobs"
     SET "State" = 'Running', "LockedBy" = {lockedBy}, "LockedUntil" = {lockedUntil}, "Attempts" = "Attempts" + 1
     WHERE "Id" = (
       SELECT j."Id"
       FROM "Jobs" AS j
       WHERE j."State" = 'Queued'
         AND j."RunAfter" <= {now}
         AND (j."LockKey" IS NULL
              OR NOT EXISTS (SELECT 1 FROM "Jobs" AS r WHERE r."State" = 'Running' AND r."LockKey" = j."LockKey"))
       ORDER BY j."RunAfter", j."Id"
       LIMIT 1
       FOR UPDATE SKIP LOCKED)
     RETURNING *
     """;

  /// <inheritdoc />
  public bool IsDeadlock(DbException exception) =>
    exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected };

  #endregion
}
