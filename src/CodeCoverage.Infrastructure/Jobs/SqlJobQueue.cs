// ------------------------------------------------------------------------------------------------
// <copyright file="SqlJobQueue.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;
using System.Diagnostics;
using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   <see cref="IJobQueue" /> over the <c>Jobs</c> table, portable between PostgreSQL and SQL Server.
/// </summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="currentOrganization">The organization of the scope, stamped on the jobs that take it.</param>
/// <param name="clock">The clock that dates the jobs.</param>
/// <param name="configuration">
///   The running configuration, read at each insertion for <c>Processing:JobMaxAttempts</c>.
/// </param>
/// <param name="dialect">The SQL of the configured engine that claims the next job.</param>
/// <param name="wakeUpSignal">The signal that wakes the worker of this process once a job is committed.</param>
/// <param name="meter">The metrics of the application, which count the dead jobs.</param>
/// <remarks>
///   <para>
///     A job is always inserted in the transaction of the change that triggers it: the repositories
///     call <see cref="EnqueueInTransactionAsync" /> in their own transaction, and
///     <see cref="EnqueueAsync" /> opens one of its own for the isolated triggers.
///   </para>
///   <para>
///     Before anything else, a request gets its <see cref="JobPlacement" />: the organization is stamped on
///     the job and both keys are prefixed by the lane. No caller can forget it, and the same business key
///     in two organizations neither merges nor serializes.
///   </para>
///   <para>
///     With a <see cref="JobRequest.DedupKey" />, the queued twin is looked up first. When there is
///     one, it is updated under the condition <c>State = 'Queued'</c>: its row stays locked until the
///     commit, so no worker can claim it before the change is visible, and a twin claimed in the
///     meantime makes the request queue a new job instead. When there is none, the job is inserted
///     under a savepoint: if a concurrent transaction queued the twin first, the unique index
///     <see cref="JobRecordConfiguration.DedupIndexName" /> refuses the insertion, the savepoint is
///     rolled back, and the request is merged into that twin.
///   </para>
///   <para>
///     A running twin never blocks a new job: the index only covers the queued jobs, so no change is
///     lost.
///   </para>
///   <para>
///     A worker claims a job with <see cref="ClaimNextAsync" />, through the <see cref="IJobClaimDialect" />
///     of the engine, then holds it for <see cref="LeaseDuration" />. Every later write of the worker
///     (<see cref="CompleteAsync" />, <see cref="FailAsync" />, <see cref="ExtendLeaseAsync" />) is
///     conditioned on <c>LockedBy = @me AND State = 'Running'</c>: a worker that lost its lease writes
///     nothing.
///   </para>
///   <para>
///     A worker that cannot complete a job yet gives it back with <see cref="RescheduleAsync" />, without
///     consuming its attempt. The <see cref="LeaseSweeper" /> gives back the jobs of the workers that
///     disappeared with <see cref="SweepExpiredLeasesAsync" />.
///   </para>
/// </remarks>
internal sealed class SqlJobQueue(
  CodeCoverageDbContext context,
  ICurrentOrganization currentOrganization,
  IClock clock,
  IConfiguration configuration,
  IJobClaimDialect dialect,
  JobWakeUpSignal wakeUpSignal,
  CodeCoverageMeter meter) : IJobQueue
{
  #region Fields

  /// <summary>How long a worker holds a job it claimed, unless it extends its lease.</summary>
  public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

  /// <summary>How many expired leases a sweep handles at most.</summary>
  public const int SweepBatchSize = 100;

  /// <summary>How many times a request tries to merge or insert, or a worker to claim, before giving up.</summary>
  private const int MaxRounds = 3;

  /// <summary>The savepoint that guards the insertion of a deduplicated job.</summary>
  private const string SavepointName = "CodeCoverageEnqueue";

  /// <summary>The attempts of a job when <c>Processing:JobMaxAttempts</c> is missing or invalid.</summary>
  private static readonly int DefaultMaxAttempts = int.Parse(
    SettingDefinitions.Find(SettingDefinitions.ProcessingJobMaxAttempts)!.Default!,
    CultureInfo.InvariantCulture
  );

  #endregion

  #region Methods

  /// <summary>Tells whether the engine chose a statement as the victim of a deadlock.</summary>
  /// <param name="exception">The failure of the statement.</param>
  /// <returns><see langword="true" /> when the statement was rolled back to break a deadlock.</returns>
  internal bool IsDeadlock(DbException exception) => dialect.IsDeadlock(exception);

  /// <summary>Claims the next job that may run, for a worker.</summary>
  /// <param name="lockedBy">The worker that claims the job.</param>
  /// <param name="cancellationToken">A token to cancel the claim.</param>
  /// <returns>
  ///   The claimed job, now <see cref="JobState.Running" />, held by <paramref name="lockedBy" /> for
  ///   <see cref="LeaseDuration" /> and with one more attempt; or <see langword="null" /> when no job
  ///   may run now.
  /// </returns>
  /// <exception cref="ArgumentException">
  ///   <paramref name="lockedBy" /> is <see langword="null" />, empty or white space.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="lockedBy" /> is longer than <see cref="JobRecord.LockedByMaxLength" />.
  /// </exception>
  /// <remarks>
  ///   <para>
  ///     The claim is one atomic statement, run outside any transaction: two workers never claim the
  ///     same job, and the jobs of a <c>LockKey</c> run one at a time.
  ///   </para>
  ///   <para>
  ///     Two workers may still claim two jobs of the same key at the same time: the unique index
  ///     <see cref="JobRecordConfiguration.LockIndexName" /> refuses the second claim, which is absorbed
  ///     and tried again; the next attempt sees the running job and passes over its key. A claim chosen
  ///     as the victim of a deadlock is tried again as well. After a few refused attempts, the method
  ///     claims nothing, and the worker tries again later.
  ///   </para>
  /// </remarks>
  internal async Task<JobRecord?> ClaimNextAsync(string lockedBy, CancellationToken cancellationToken)
  {
    CheckWorker(lockedBy);

    for (var round = 1; round <= MaxRounds; round++)
    {
      var now = clock.UtcNow;

      try
      {
        // Not composed (no FirstOrDefault): the statement is an UPDATE, run by the engine as written.
        var claimed = await context.Jobs
                                   .FromSql(dialect.ClaimNext(lockedBy, now, now + LeaseDuration))
                                   .AsNoTracking()
                                   .ToListAsync(cancellationToken)
                                   .ConfigureAwait(false);

        return claimed.SingleOrDefault();
      }
      catch (Exception exception) when (UniqueIndexViolation.Is(exception, JobRecordConfiguration.LockIndexName))
      {
        // A job of the same key started concurrently: the statement wrote nothing, try the next job.
      }
      catch (DbException exception) when (dialect.IsDeadlock(exception))
      {
        // Chosen as the victim of a deadlock between claiming workers: the statement wrote nothing.
      }
    }

    return null;
  }

  /// <summary>Marks a job held by a worker as succeeded.</summary>
  /// <param name="jobId">The identifier of the job.</param>
  /// <param name="lockedBy">The worker that claimed the job.</param>
  /// <param name="cancellationToken">A token to cancel the update.</param>
  /// <returns>
  ///   <see langword="true" /> when the job was completed; <see langword="false" /> when the worker no
  ///   longer holds it (lease lost, job claimed again by another worker): nothing is written.
  /// </returns>
  /// <exception cref="ArgumentException">
  ///   <paramref name="lockedBy" /> is <see langword="null" />, empty or white space.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="lockedBy" /> is longer than <see cref="JobRecord.LockedByMaxLength" />.
  /// </exception>
  internal async Task<bool> CompleteAsync(long jobId, string lockedBy, CancellationToken cancellationToken)
  {
    CheckWorker(lockedBy);

    var now = clock.UtcNow;
    var updated = await Held(jobId, lockedBy)
                        .ExecuteUpdateAsync(
                          s => s.SetProperty(j => j.State, JobState.Succeeded)
                                .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null)
                                .SetProperty(j => j.CompletedAt, now),
                          cancellationToken
                        )
                        .ConfigureAwait(false);

    return updated == 1;
  }

  /// <summary>Renews the lease of a worker on a job it holds.</summary>
  /// <param name="jobId">The identifier of the job.</param>
  /// <param name="lockedBy">The worker that claimed the job.</param>
  /// <param name="cancellationToken">A token to cancel the update.</param>
  /// <returns>
  ///   <see langword="true" /> when the lease now ends <see cref="LeaseDuration" /> from now;
  ///   <see langword="false" /> when the worker no longer holds the job: nothing is written.
  /// </returns>
  /// <exception cref="ArgumentException">
  ///   <paramref name="lockedBy" /> is <see langword="null" />, empty or white space.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="lockedBy" /> is longer than <see cref="JobRecord.LockedByMaxLength" />.
  /// </exception>
  internal async Task<bool> ExtendLeaseAsync(long jobId, string lockedBy, CancellationToken cancellationToken)
  {
    CheckWorker(lockedBy);

    DateTimeOffset? lockedUntil = clock.UtcNow + LeaseDuration;
    var updated = await Held(jobId, lockedBy)
                        .ExecuteUpdateAsync(s => s.SetProperty(j => j.LockedUntil, lockedUntil), cancellationToken)
                        .ConfigureAwait(false);

    return updated == 1;
  }

  /// <summary>Records the failure of a job held by a worker.</summary>
  /// <param name="jobId">The identifier of the job.</param>
  /// <param name="lockedBy">The worker that claimed the job.</param>
  /// <param name="error">The error of the attempt.</param>
  /// <param name="retryAt">
  ///   When the job may run again, in UTC; or <see langword="null" /> for a permanent refusal, never
  ///   retried.
  /// </param>
  /// <param name="cancellationToken">A token to cancel the update.</param>
  /// <returns>
  ///   <see langword="true" /> when the failure was recorded; <see langword="false" /> when the worker no
  ///   longer holds the job: nothing is written.
  /// </returns>
  /// <exception cref="ArgumentException">
  ///   <paramref name="lockedBy" /> or <paramref name="error" /> is <see langword="null" />, empty or white
  ///   space.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="lockedBy" /> is longer than <see cref="JobRecord.LockedByMaxLength" />.
  /// </exception>
  /// <remarks>
  ///   <list type="bullet">
  ///     <item>Without <paramref name="retryAt" />, the job is <see cref="JobState.Failed" />.</item>
  ///     <item>
  ///       With <paramref name="retryAt" />, a job that used all its attempts is
  ///       <see cref="JobState.Dead" /> (counted by <see cref="CodeCoverageMeter.JobsDeadTotal" />); any other
  ///       job is queued again from <paramref name="retryAt" />.
  ///     </item>
  ///     <item>
  ///       When a queued twin with the same <c>DedupKey</c> exists, the unique index
  ///       <see cref="JobRecordConfiguration.DedupIndexName" /> refuses to queue the job again: the
  ///       twin does the same work, so the job is absorbed (<see cref="JobState.Succeeded" />, its
  ///       error kept).
  ///     </item>
  ///   </list>
  /// </remarks>
  internal async Task<bool> FailAsync(
    long jobId,
    string lockedBy,
    string error,
    DateTimeOffset? retryAt,
    CancellationToken cancellationToken)
  {
    CheckWorker(lockedBy);
    ArgumentException.ThrowIfNullOrWhiteSpace(error);

    var now = clock.UtcNow;

    if (retryAt is not { } runAfter)
    {
      return await EndAsync(Held(jobId, lockedBy), JobState.Failed, error, now, cancellationToken).ConfigureAwait(false);
    }

    var exhausted = Held(jobId, lockedBy).Where(static j => j.Attempts >= j.MaxAttempts);

    if (await EndAsync(exhausted, JobState.Dead, error, now, cancellationToken).ConfigureAwait(false))
    {
      meter.JobDead();
      return true;
    }

    try
    {
      var updated = await Held(jobId, lockedBy)
                          .ExecuteUpdateAsync(
                            s => s.SetProperty(j => j.State, JobState.Queued)
                                  .SetProperty(j => j.RunAfter, runAfter)
                                  .SetProperty(j => j.LockedBy, (string?)null)
                                  .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null)
                                  .SetProperty(j => j.LastError, error),
                            cancellationToken
                          )
                          .ConfigureAwait(false);

      return updated == 1;
    }
    catch (Exception exception) when (UniqueIndexViolation.Is(exception, JobRecordConfiguration.DedupIndexName))
    {
      // A queued twin does the same work: the job is absorbed instead of being queued twice.
      return await EndAsync(Held(jobId, lockedBy), JobState.Succeeded, error, now, cancellationToken)
               .ConfigureAwait(false);
    }
  }

  /// <summary>Measures the queue, for the gauges of the jobs.</summary>
  /// <param name="cancellationToken">A token to cancel the queries.</param>
  /// <returns>
  ///   The number of queued jobs by type, and how long the oldest job that may run (<c>RunAfter</c> passed) has
  ///   waited since its <c>RunAfter</c>: a debounced or delayed job does not wait before its time.
  /// </returns>
  internal async Task<JobQueueSnapshot> MeasureAsync(CancellationToken cancellationToken)
  {
    var now = clock.UtcNow;
    var queued = context.Jobs.AsNoTracking().Where(static j => j.State == JobState.Queued);

    var byType = await queued.GroupBy(static j => j.Type)
                             .Select(static g => new { Type = g.Key, Count = g.LongCount() })
                             .ToDictionaryAsync(static g => g.Type, static g => g.Count, StringComparer.Ordinal, cancellationToken)
                             .ConfigureAwait(false);
    var oldest = await queued.Where(j => j.RunAfter <= now)
                             .OrderBy(static j => j.RunAfter)
                             .Select(static j => (DateTimeOffset?)j.RunAfter)
                             .FirstOrDefaultAsync(cancellationToken)
                             .ConfigureAwait(false);
    var age = oldest is { } runAfter ? Math.Max(0, (now - runAfter).TotalSeconds) : 0;

    return new JobQueueSnapshot(byType, age);
  }

  /// <summary>
  ///   Gives back a job held by a worker that cannot complete it yet, without consuming its attempt.
  /// </summary>
  /// <param name="jobId">The identifier of the job.</param>
  /// <param name="lockedBy">The worker that claimed the job.</param>
  /// <param name="runAfter">When the job may run again, in UTC.</param>
  /// <param name="reason">Why the job waits, kept as its last error.</param>
  /// <param name="cancellationToken">A token to cancel the update.</param>
  /// <returns>
  ///   <see langword="true" /> when the job was given back; <see langword="false" /> when the worker no
  ///   longer holds it: nothing is written.
  /// </returns>
  /// <exception cref="ArgumentException">
  ///   <paramref name="lockedBy" /> or <paramref name="reason" /> is <see langword="null" />, empty or white
  ///   space.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="lockedBy" /> is longer than <see cref="JobRecord.LockedByMaxLength" />.
  /// </exception>
  /// <remarks>
  ///   <para>
  ///     For a handler that asks to run later (a commit waiting for the uploads of an ancestor, a GitHub
  ///     rate limit) or a host that stops while the job runs: the job is queued again from
  ///     <paramref name="runAfter" /> and its attempt is given back (<c>Attempts - 1</c>), so waiting never
  ///     leads it to <see cref="JobState.Dead" />.
  ///   </para>
  ///   <para>
  ///     When a queued twin with the same <c>DedupKey</c> exists, the job is absorbed by the twin
  ///     (<see cref="JobState.Succeeded" />, its reason kept), as in <see cref="FailAsync" />.
  ///   </para>
  /// </remarks>
  internal async Task<bool> RescheduleAsync(
    long jobId,
    string lockedBy,
    DateTimeOffset runAfter,
    string reason,
    CancellationToken cancellationToken)
  {
    CheckWorker(lockedBy);
    ArgumentException.ThrowIfNullOrWhiteSpace(reason);

    try
    {
      // A running job always has one attempt at least: the claim counted it.
      var updated = await Held(jobId, lockedBy)
                          .Where(static j => j.Attempts > 0)
                          .ExecuteUpdateAsync(
                            s => s.SetProperty(j => j.State, JobState.Queued)
                                  .SetProperty(j => j.RunAfter, runAfter)
                                  .SetProperty(j => j.Attempts, j => j.Attempts - 1)
                                  .SetProperty(j => j.LockedBy, (string?)null)
                                  .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null)
                                  .SetProperty(j => j.LastError, reason),
                            cancellationToken
                          )
                          .ConfigureAwait(false);

      return updated == 1;
    }
    catch (Exception exception) when (UniqueIndexViolation.Is(exception, JobRecordConfiguration.DedupIndexName))
    {
      // A queued twin does the same work: the job is absorbed instead of being queued twice.
      return await EndAsync(Held(jobId, lockedBy), JobState.Succeeded, reason, clock.UtcNow, cancellationToken)
               .ConfigureAwait(false);
    }
  }

  /// <summary>Gives back the running jobs whose lease expired: their worker disappeared.</summary>
  /// <param name="cancellationToken">A token to cancel the sweep.</param>
  /// <returns>How many jobs were queued again, absorbed or marked dead.</returns>
  /// <remarks>
  ///   <para>
  ///     At most <see cref="SweepBatchSize" /> jobs per call, the oldest lease first. Each job is handled by
  ///     its own update, conditioned on <c>State = 'Running'</c> and an expired lease: a worker that renewed
  ///     its lease in the meantime keeps its job.
  ///   </para>
  ///   <list type="bullet">
  ///     <item>
  ///       A job that used all its attempts is <see cref="JobState.Dead" /> (counted by
  ///       <see cref="CodeCoverageMeter.JobsDeadTotal" />): a job that brings its worker down each time it runs
  ///       stops there.
  ///     </item>
  ///     <item>
  ///       Any other job is queued again at once, its attempt consumed: the worker may have done part of
  ///       the work.
  ///     </item>
  ///     <item>
  ///       The requeuing takes the deduplication into account: when a queued twin with the same
  ///       <c>DedupKey</c> exists, the unique index <see cref="JobRecordConfiguration.DedupIndexName" />
  ///       refuses it, and the job is absorbed by the twin (<see cref="JobState.Succeeded" />).
  ///     </item>
  ///   </list>
  /// </remarks>
  internal async Task<int> SweepExpiredLeasesAsync(CancellationToken cancellationToken)
  {
    var now = clock.UtcNow;
    var expired = await context.Jobs
                               .AsNoTracking()
                               .Where(j => j.State == JobState.Running && j.LockedUntil < now)
                               .OrderBy(static j => j.LockedUntil)
                               .ThenBy(static j => j.Id)
                               .Select(static j => new { j.Id, j.LockedBy })
                               .Take(SweepBatchSize)
                               .ToListAsync(cancellationToken)
                               .ConfigureAwait(false);
    var swept = 0;

    foreach (var job in expired)
    {
      if (await SweepAsync(job.Id, job.LockedBy, now, cancellationToken).ConfigureAwait(false))
      {
        swept++;
      }
    }

    return swept;
  }

  /// <summary>
  ///   Queues a job in the current transaction of a context, without committing it: the job is saved
  ///   with the change of the caller, or not at all.
  /// </summary>
  /// <param name="transactionContext">
  ///   The context of the caller, with an open transaction and its own changes already saved.
  /// </param>
  /// <param name="request">The job to run.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>
  ///   The identifier of the queued job: a new job, or the queued twin the request was merged into.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="transactionContext" /> or <paramref name="request" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="InvalidOperationException">
  ///   <paramref name="transactionContext" /> has no open transaction, the twin kept changing state, or the
  ///   scope does not allow the organization of the request.
  /// </exception>
  /// <remarks>
  ///   The insertion saves the pending changes of <paramref name="transactionContext" />: the repository
  ///   saves its own changes first, so that only the job is written here.
  /// </remarks>
  internal async Task<long> EnqueueInTransactionAsync(
    CodeCoverageDbContext transactionContext,
    JobRequest request,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(transactionContext);
    ArgumentNullException.ThrowIfNull(request);

    return (await EnqueueOrMergeAsync(transactionContext, request, cancellationToken).ConfigureAwait(false)).Id;
  }

  /// <summary>Queues a job in the transaction of the caller, or merges it into its queued twin.</summary>
  /// <param name="transactionContext">The context of the caller, whose transaction is begun.</param>
  /// <param name="request">The job to run.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The identifier of the job, and whether the request was merged into its queued twin.</returns>
  /// <exception cref="InvalidOperationException">
  ///   No transaction is begun, the twin kept changing state, or the current scope does not allow the organization
  ///   of the request.
  /// </exception>
  private async Task<(long Id, bool Merged)> EnqueueOrMergeAsync(
    CodeCoverageDbContext transactionContext,
    JobRequest request,
    CancellationToken cancellationToken)
  {
    var placement = JobPlacement.Of(request, currentOrganization.IsKnown ? currentOrganization.Id : null);
    var transaction = transactionContext.Database.CurrentTransaction
                      ?? throw new InvalidOperationException(
                        "A job is queued in the transaction of the change that triggers it: begin a transaction first."
                      );
    var now = clock.UtcNow;

    if (placement.DedupKey is null)
    {
      return (await InsertAsync(transactionContext, request, placement, now, cancellationToken).ConfigureAwait(false), false);
    }

    for (var round = 1; round <= MaxRounds; round++)
    {
      var twin = await transactionContext.Jobs
                                         .AsNoTracking()
                                         .Where(j => j.DedupKey == placement.DedupKey && j.State == JobState.Queued)
                                         .Select(static j => new { j.Id, j.FirstQueuedAt })
                                         .FirstOrDefaultAsync(cancellationToken)
                                         .ConfigureAwait(false);

      if (twin is not null)
      {
        if (await MergeAsync(transactionContext, twin.Id, twin.FirstQueuedAt, request, now, cancellationToken)
              .ConfigureAwait(false))
        {
          return (twin.Id, true);
        }

        // Claimed between the read and the update: the next round queues a new job.
        continue;
      }

      var inserted = await TryInsertUnderSavepointAsync(transactionContext, transaction, request, placement, now, cancellationToken)
                       .ConfigureAwait(false);

      if (inserted is { } id)
      {
        return (id, false);
      }
    }

    throw new InvalidOperationException(
      $"The job '{request}' could not be queued: its queued twin kept changing state. Try again."
    );
  }

  /// <summary>Queues a job in a transaction of its own, and wakes a worker of this process.</summary>
  /// <param name="request">The job to run.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The identifier of the job, and whether the request was merged into its queued twin.</returns>
  private async Task<(long Id, bool Merged)> EnqueueAloneAsync(JobRequest request, CancellationToken cancellationToken)
  {
    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      var queued = await EnqueueOrMergeAsync(context, request, cancellationToken).ConfigureAwait(false);
      await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

      // Committed: a worker of this process may claim the job at once instead of at its next poll.
      wakeUpSignal.Notify(queued.Id);

      return queued;
    }
  }

  /// <summary>Checks the identifier of a worker.</summary>
  /// <param name="lockedBy">The identifier of the worker.</param>
  /// <exception cref="ArgumentException">
  ///   <paramref name="lockedBy" /> is <see langword="null" />, empty or white space.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="lockedBy" /> is longer than <see cref="JobRecord.LockedByMaxLength" />.
  /// </exception>
  private static void CheckWorker(string lockedBy)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(lockedBy);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(lockedBy.Length, JobRecord.LockedByMaxLength, nameof(lockedBy));
  }

  /// <summary>Ends a job for good, keeping the worker that ran it.</summary>
  /// <param name="job">The job to end, while the worker holds it.</param>
  /// <param name="state">The final state.</param>
  /// <param name="error">The error of the last attempt.</param>
  /// <param name="now">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the update.</param>
  /// <returns><see langword="true" /> when the job was ended; otherwise <see langword="false" />.</returns>
  private static async Task<bool> EndAsync(
    IQueryable<JobRecord> job,
    JobState state,
    string error,
    DateTimeOffset now,
    CancellationToken cancellationToken)
  {
    var updated = await job.ExecuteUpdateAsync(
                             s => s.SetProperty(j => j.State, state)
                                   .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null)
                                   .SetProperty(j => j.LastError, error)
                                   .SetProperty(j => j.CompletedAt, now),
                             cancellationToken
                           )
                           .ConfigureAwait(false);

    return updated == 1;
  }

  /// <summary>Tells when a job may run, given its debounce window.</summary>
  /// <param name="request">The request.</param>
  /// <param name="now">The current time.</param>
  /// <param name="firstQueuedAt">When the job (or its twin) was first queued.</param>
  /// <returns>The requested time, but never later than the maximum delay after the first queuing.</returns>
  private static DateTimeOffset RunAfter(JobRequest request, DateTimeOffset now, DateTimeOffset firstQueuedAt)
  {
    var runAfter = request.RunAfter ?? now;

    if (request.MaxDelay is { } maxDelay && runAfter > firstQueuedAt + maxDelay)
    {
      runAfter = firstQueuedAt + maxDelay;
    }

    return runAfter;
  }

  /// <summary>Gives the correlation identifier of the current operation.</summary>
  /// <returns>The trace identifier of the current activity, or a new identifier when there is none.</returns>
  private static string CurrentCorrelationId() =>
    Activity.Current is { } activity && activity.TraceId != default
      ? activity.TraceId.ToHexString()
      : Guid.NewGuid().ToString("N");

  /// <summary>Merges a request into its queued twin, locking the row of the twin until the commit.</summary>
  /// <param name="transactionContext">The context of the caller.</param>
  /// <param name="twinId">The identifier of the twin.</param>
  /// <param name="firstQueuedAt">When the twin was first queued.</param>
  /// <param name="request">The request.</param>
  /// <param name="now">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the update.</param>
  /// <returns>
  ///   <see langword="true" /> when the twin was still queued; <see langword="false" /> when a worker
  ///   claimed it in the meantime.
  /// </returns>
  private static async Task<bool> MergeAsync(
    CodeCoverageDbContext transactionContext,
    long twinId,
    DateTimeOffset firstQueuedAt,
    JobRequest request,
    DateTimeOffset now,
    CancellationToken cancellationToken)
  {
    var twin = transactionContext.Jobs.Where(j => j.Id == twinId && j.State == JobState.Queued);
    int updated;

    if (request.Coalesce)
    {
      // Changes nothing, but locks the row: the twin cannot be claimed before this change is committed.
      updated = await twin.ExecuteUpdateAsync(static s => s.SetProperty(j => j.RunAfter, j => j.RunAfter), cancellationToken)
                          .ConfigureAwait(false);
    }
    else
    {
      var runAfter = RunAfter(request, now, firstQueuedAt);
      updated = await twin.ExecuteUpdateAsync(s => s.SetProperty(j => j.RunAfter, runAfter), cancellationToken)
                          .ConfigureAwait(false);
    }

    return updated == 1;
  }

  /// <summary>Inserts a job and saves it in the current transaction.</summary>
  /// <param name="transactionContext">The context of the caller.</param>
  /// <param name="request">The request.</param>
  /// <param name="placement">The organization and the prefixed keys of the job.</param>
  /// <param name="now">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The identifier of the new job.</returns>
  private async Task<long> InsertAsync(
    CodeCoverageDbContext transactionContext,
    JobRequest request,
    JobPlacement placement,
    DateTimeOffset now,
    CancellationToken cancellationToken)
  {
    var job = new JobRecord
    {
      OrganizationId = placement.OrganizationId,
      Type = request.Type,
      PayloadJson = request.PayloadJson,
      LockKey = placement.LockKey,
      DedupKey = placement.DedupKey,
      State = JobState.Queued,
      Attempts = 0,
      MaxAttempts = ReadMaxAttempts(),
      RunAfter = RunAfter(request, now, now),
      FirstQueuedAt = now,
      CorrelationId = CurrentCorrelationId(),
      CreatedAt = now,
    };

    transactionContext.Jobs.Add(job);

    try
    {
      await transactionContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

      return job.Id;
    }
    finally
    {
      // Never flushed again by a later save of the scope, whatever the outcome.
      transactionContext.Entry(job).State = EntityState.Detached;
    }
  }

  /// <summary>Selects a job while a worker holds it.</summary>
  /// <param name="jobId">The identifier of the job.</param>
  /// <param name="lockedBy">The worker that claimed the job.</param>
  /// <returns>The job, when it is still running under <paramref name="lockedBy" />; otherwise nothing.</returns>
  private IQueryable<JobRecord> Held(long jobId, string lockedBy) =>
    context.Jobs.Where(j => j.Id == jobId && j.LockedBy == lockedBy && j.State == JobState.Running);

  /// <summary>Reads how many attempts a new job gets.</summary>
  /// <returns><c>Processing:JobMaxAttempts</c> when it is a positive number; otherwise its default.</returns>
  private int ReadMaxAttempts() =>
    int.TryParse(
      configuration[SettingDefinitions.ProcessingJobMaxAttempts],
      NumberStyles.Integer,
      CultureInfo.InvariantCulture,
      out var maxAttempts
    )
    && maxAttempts > 0
      ? maxAttempts
      : DefaultMaxAttempts;

  /// <summary>Gives back one running job whose lease expired.</summary>
  /// <param name="jobId">The identifier of the job.</param>
  /// <param name="lockedBy">The worker that held the job.</param>
  /// <param name="now">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the update.</param>
  /// <returns>
  ///   <see langword="true" /> when the job was queued again, absorbed or marked dead;
  ///   <see langword="false" /> when its worker renewed the lease or ended the job in the meantime.
  /// </returns>
  private async Task<bool> SweepAsync(long jobId, string? lockedBy, DateTimeOffset now, CancellationToken cancellationToken)
  {
    var error = $"The lease of the worker '{lockedBy}' expired before the job ended.";
    var job = context.Jobs.Where(j => j.Id == jobId && j.State == JobState.Running && j.LockedUntil < now);

    if (await EndAsync(job.Where(static j => j.Attempts >= j.MaxAttempts), JobState.Dead, error, now, cancellationToken)
          .ConfigureAwait(false))
    {
      meter.JobDead();
      return true;
    }

    try
    {
      var updated = await job.ExecuteUpdateAsync(
                               s => s.SetProperty(j => j.State, JobState.Queued)
                                     .SetProperty(j => j.RunAfter, now)
                                     .SetProperty(j => j.LockedBy, (string?)null)
                                     .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null)
                                     .SetProperty(j => j.LastError, error),
                               cancellationToken
                             )
                             .ConfigureAwait(false);

      return updated == 1;
    }
    catch (Exception exception) when (UniqueIndexViolation.Is(exception, JobRecordConfiguration.DedupIndexName))
    {
      // A queued twin does the same work: the expired job is absorbed instead of being queued twice.
      return await EndAsync(job, JobState.Succeeded, error, now, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Inserts a deduplicated job under a savepoint, unless its twin was queued concurrently.</summary>
  /// <param name="transactionContext">The context of the caller.</param>
  /// <param name="transaction">The current transaction of the caller.</param>
  /// <param name="request">The request.</param>
  /// <param name="placement">The organization and the prefixed keys of the job.</param>
  /// <param name="now">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>
  ///   The identifier of the new job, or <see langword="null" /> when the unique index refused it: a
  ///   twin was queued concurrently, and the transaction is back at the savepoint.
  /// </returns>
  private async Task<long?> TryInsertUnderSavepointAsync(
    CodeCoverageDbContext transactionContext,
    IDbContextTransaction transaction,
    JobRequest request,
    JobPlacement placement,
    DateTimeOffset now,
    CancellationToken cancellationToken)
  {
    await transaction.CreateSavepointAsync(SavepointName, cancellationToken).ConfigureAwait(false);

    try
    {
      return await InsertAsync(transactionContext, request, placement, now, cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, JobRecordConfiguration.DedupIndexName))
    {
      // PostgreSQL aborts the transaction on the violation: the savepoint makes it usable again.
      await transaction.RollbackToSavepointAsync(SavepointName, cancellationToken).ConfigureAwait(false);

      return null;
    }
  }

  #endregion

  #region Implementation of IJobQueue

  /// <inheritdoc />
  /// <exception cref="InvalidOperationException">The context of the scope already has an open transaction.</exception>
  public async Task<long> EnqueueAsync(JobRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return (await EnqueueAloneAsync(request, cancellationToken).ConfigureAwait(false)).Id;
  }

  /// <inheritdoc />
  public async Task<bool> TryEnqueueAsync(JobRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return !(await EnqueueAloneAsync(request, cancellationToken).ConfigureAwait(false)).Merged;
  }

  /// <inheritdoc />
  public async Task<JobRetryResult> RetryAsync(long id, CancellationToken cancellationToken)
  {
    var now = clock.UtcNow;
    int updated;

    try
    {
      // One conditional update: a job claimed, retried or purged in the meantime is left as it is.
      updated = await context.Jobs
                             .Where(j => j.Id == id && (j.State == JobState.Failed || j.State == JobState.Dead))
                             .ExecuteUpdateAsync(
                               s => s.SetProperty(j => j.State, JobState.Queued)
                                     .SetProperty(j => j.Attempts, 0)
                                     .SetProperty(j => j.RunAfter, now)
                                     .SetProperty(j => j.FirstQueuedAt, now)
                                     .SetProperty(j => j.LockedBy, (string?)null)
                                     .SetProperty(j => j.LockedUntil, (DateTimeOffset?)null)
                                     .SetProperty(j => j.CompletedAt, (DateTimeOffset?)null),
                               cancellationToken
                             )
                             .ConfigureAwait(false);
    }
    catch (Exception exception) when (UniqueIndexViolation.Is(exception, JobRecordConfiguration.DedupIndexName))
    {
      // A queued twin already does the same work.
      return JobRetryResult.NotRetryable;
    }

    if (updated == 1)
    {
      wakeUpSignal.Notify(id);

      return JobRetryResult.Retried;
    }

    return await context.Jobs.AnyAsync(j => j.Id == id, cancellationToken).ConfigureAwait(false)
             ? JobRetryResult.NotRetryable
             : JobRetryResult.NotFound;
  }

  #endregion
}
