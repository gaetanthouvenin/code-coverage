// ------------------------------------------------------------------------------------------------
// <copyright file="IJobClaimDialect.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   The SQL of a database engine that claims the next job of the <c>Jobs</c> table in one atomic
///   statement.
/// </summary>
/// <remarks>
///   <para>
///     The only raw SQL of the queue (ADR-005): EF Core cannot express a locking read that skips the
///     rows locked by the other workers. Each migrations assembly provides the dialect of its engine
///     (<c>FOR UPDATE SKIP LOCKED</c> under PostgreSQL, <c>WITH (ROWLOCK, UPDLOCK, READPAST)</c> under
///     SQL Server) and registers it with its <c>Add…Provider()</c> method, keyed by its
///     <see cref="DatabaseProvider" />.
///   </para>
///   <para>
///     The statement takes the first queued job whose <c>RunAfter</c> has come, ordered by
///     <c>RunAfter</c> then <c>Id</c>, whose <c>LockKey</c> has no running job, and turns it into a
///     running job held by the worker: <c>State = 'Running'</c>, <c>LockedBy</c>, <c>LockedUntil</c>
///     and <c>Attempts + 1</c>. It returns every column of the claimed row, or no row.
///   </para>
///   <para>
///     The time is a parameter, never the clock of the server: <c>RunAfter</c> and <c>LockedUntil</c>
///     are written from the clock of the application everywhere else, so they are compared with it.
///   </para>
/// </remarks>
internal interface IJobClaimDialect
{
  #region Properties

  /// <summary>Gets the database engine the SQL is written for.</summary>
  DatabaseProvider Provider { get; }

  #endregion

  #region Methods

  /// <summary>Builds the statement that claims the next job.</summary>
  /// <param name="lockedBy">The worker that claims the job.</param>
  /// <param name="now">The current time, in UTC.</param>
  /// <param name="lockedUntil">When the lease of the worker ends, in UTC.</param>
  /// <returns>
  ///   The statement, whose interpolated values are sent as parameters (<c>FromSql</c>), never inlined.
  /// </returns>
  FormattableString ClaimNext(string lockedBy, DateTimeOffset now, DateTimeOffset lockedUntil);

  /// <summary>Tells whether the engine chose a statement as the victim of a deadlock.</summary>
  /// <param name="exception">The error of the engine.</param>
  /// <returns>
  ///   <see langword="true" /> when the statement was rolled back to break a deadlock, and may be run
  ///   again; otherwise <see langword="false" />.
  /// </returns>
  /// <remarks>
  ///   Many workers claiming at once may deadlock on the indexes of the table: the claim of the victim
  ///   wrote nothing, and the queue tries again.
  /// </remarks>
  bool IsDeadlock(DbException exception);

  #endregion
}
