// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReportRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence.Configurations;
using CodeCoverage.Infrastructure.Commits;
using CodeCoverage.Infrastructure.Jobs;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>
///   Reads and writes the <c>UploadCoverages</c>, <c>CommitSessions</c>, <c>CommitReports</c> and <c>CommitFlagTotals</c>
///   tables, under the query filter of the organization.
/// </summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="jobQueue">The queue that takes a job in the transaction of the change.</param>
/// <param name="wakeUpSignal">The signal that wakes the worker of this process once a job is committed.</param>
/// <remarks>
///   An upload and a commit are written by the context that read them: the shadow column of the organization does not
///   travel with an instance (ADR-013). The rows of a failed save are forgotten, so that a later save of the scope does
///   not repeat them.
/// </remarks>
internal sealed class CommitReportRepository(
  CodeCoverageDbContext context,
  SqlJobQueue jobQueue,
  JobWakeUpSignal wakeUpSignal) : ICommitReportRepository
{
  #region Fields

  /// <summary>The message of the refusal of an upload that this context did not read.</summary>
  private const string UploadNotReadMessage = "An upload must be read in this scope before its result can be saved.";

  /// <summary>The message of the refusal of a commit that does not carry a finalization read in this context.</summary>
  private const string NotFinalizedMessage = "A commit must be read in this scope and carry a finalization before it can be saved.";

  /// <summary>The message of the refusal of a row of another commit.</summary>
  private const string ForeignCommitMessage = "Every session and every total must belong to the commit.";

  /// <summary>The message of the refusal of a branch of another repository.</summary>
  private const string ForeignBranchMessage = "The branch must belong to the repository of the commit.";

  /// <summary>The message of the refusal of a finalization that lost a race; it is sent to the caller.</summary>
  private const string ConcurrentSaveMessage = "The commit was finalized in the meantime; finalize it again.";

  /// <summary>How many waiting commits a commit that completes wakes.</summary>
  private const int MaxWokenCommits = 10;

  /// <summary>How long the victim of a deadlock waits before it runs again.</summary>
  private static readonly TimeSpan DeadlockRetryDelay = TimeSpan.FromSeconds(2);

  #endregion

  #region Implementation of ICommitReportRepository

  /// <inheritdoc />
  public async Task<byte[]?> GetUploadCoverageAsync(long uploadId, CancellationToken cancellationToken) =>
    await context.UploadCoverages
                 .AsNoTracking()
                 .Where(c => c.UploadId == uploadId)
                 .Select(static c => c.Data)
                 .SingleOrDefaultAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  /// <exception cref="InvalidOperationException">The upload was not read in this scope.</exception>
  /// <exception cref="ConflictException">The upload changed in the meantime.</exception>
  /// <remarks>A job that runs again replaces the blob of its previous attempt.</remarks>
  public async Task SaveProcessedUploadAsync(Upload upload, byte[]? blob, JobRequest job, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(upload);
    ArgumentNullException.ThrowIfNull(job);

    InvalidOperationException.ThrowIf(context.Entry(upload).State is EntityState.Detached, UploadNotReadMessage);

    var uploadId = upload.Id;
    var stored = await context.UploadCoverages
                              .SingleOrDefaultAsync(c => c.UploadId == uploadId, cancellationToken)
                              .ConfigureAwait(false);
    long jobId;
    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      var written = WriteCoverage(uploadId, stored, blob);

      try
      {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        jobId = await jobQueue.EnqueueInTransactionAsync(context, job, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }
      catch
      {
        if (written is not null)
        {
          context.Entry(written).State = EntityState.Detached;
        }

        throw;
      }
    }

    // Committed: a worker of this process may claim the job at once instead of at its next poll.
    wakeUpSignal.Notify(jobId);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<Upload>> RequeueUploadsAsync(IReadOnlyList<UploadRequeue> requeues, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(requeues);

    var taken = new List<Upload>(requeues.Count);
    var jobIds = new List<long>(requeues.Count);
    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      try
      {
        foreach (var requeue in requeues)
        {
          if (await TryTakeAsync(requeue, cancellationToken).ConfigureAwait(false) is { } upload)
          {
            taken.Add(upload);
          }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var upload in taken)
        {
          jobIds.Add(await jobQueue.EnqueueInTransactionAsync(context, ProcessUploadJobs.Create(upload.PublicId), cancellationToken).ConfigureAwait(false));
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }
      catch
      {
        // Nothing is left for a later save of the scope: the uploads no longer match their rows.
        foreach (var upload in taken)
        {
          context.Entry(upload).State = EntityState.Detached;
        }

        throw;
      }
    }

    foreach (var jobId in jobIds)
    {
      wakeUpSignal.Notify(jobId);
    }

    return taken;
  }

  /// <inheritdoc />
  /// <exception cref="ConflictException">The commit cannot be read.</exception>
  public async Task<bool> TryAddReceivedUploadAsync(Upload upload, JobRequest job, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(upload);
    ArgumentNullException.ThrowIfNull(job);

    // The unique index does not hold the organization: a row for the commit of another organization would take its key.
    var commitId = upload.CommitId;

    if (!await context.Commits.AnyAsync(c => c.Id == commitId, cancellationToken).ConfigureAwait(false))
    {
      throw new ConflictException(GeneralErrors.ConcurrentChange, "The commit was changed or removed in the meantime; reload it and try again.");
    }

    long jobId;
    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      context.Uploads.Add(upload);

      try
      {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        jobId = await jobQueue.EnqueueInTransactionAsync(context, job, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }
      catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, UploadConfiguration.IdempotencyIndexName))
      {
        context.Entry(upload).State = EntityState.Detached;

        return false;
      }
      catch
      {
        context.Entry(upload).State = EntityState.Detached;

        throw;
      }
    }

    wakeUpSignal.Notify(jobId);

    return true;
  }

  /// <inheritdoc />
  /// <exception cref="InvalidOperationException">The commit was not read in this scope, or carries no finalization.</exception>
  /// <exception cref="ArgumentException">A session or a total belongs to another commit.</exception>
  /// <exception cref="ConflictException">Another finalization saved the commit in the meantime.</exception>
  /// <remarks>The context must not have an open transaction.</remarks>
  public async Task SaveFinalizedAsync(
    Commit commit,
    byte[] report,
    byte[] fileIndex,
    IReadOnlyList<Session> sessions,
    IReadOnlyList<CommitFlagTotals> flagTotals,
    Branch? branch,
    FinalizationWakeUps wakeUps,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(report);
    ArgumentNullException.ThrowIfNull(fileIndex);
    ArgumentNullException.ThrowIfNull(sessions);
    ArgumentNullException.ThrowIfNull(flagTotals);
    ArgumentNullException.ThrowIfNull(wakeUps);

    InvalidOperationException.ThrowIf(!context.Entry(commit).Property(static c => c.Version).IsModified, NotFinalizedMessage);
    ArgumentException.ThrowIf(sessions.Any(s => s.CommitId != commit.Id), ForeignCommitMessage, nameof(sessions));
    ArgumentException.ThrowIf(flagTotals.Any(t => t.CommitId != commit.Id), ForeignCommitMessage, nameof(flagTotals));
    ArgumentException.ThrowIf(branch is not null && branch.RepositoryId != commit.RepositoryId, ForeignBranchMessage, nameof(branch));

    var commitId = commit.Id;
    IReadOnlyList<long> queued = [];
    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      CommitReportRecord? stored = null;

      try
      {
        await context.CommitSessions.Where(s => s.CommitId == commitId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.CommitFlagTotals.Where(t => t.CommitId == commitId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        stored = await context.CommitReports
                              .SingleOrDefaultAsync(r => r.CommitId == commitId, cancellationToken)
                              .ConfigureAwait(false);

        stored = WriteReport(commit, stored, report, fileIndex);
        context.CommitSessions.AddRange(sessions);
        context.CommitFlagTotals.AddRange(flagTotals);

        // A branch read in this scope is tracked already and carries its change.
        if (branch is not null && context.Entry(branch).State is EntityState.Detached)
        {
          context.Branches.Add(branch);
        }
        else if (branch is not null && context.Entry(branch).State is EntityState.Unchanged)
        {
          // Unchanged, its version is still checked and moved: a push that moved its head meanwhile makes this save a
          // conflict, and a push that read the row before this save makes its own a conflict. Either way, the one run
          // again covers the new head.
          var version = context.Entry(branch).Property(static b => b.Version);

          version.CurrentValue = version.OriginalValue + 1;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // After the row of the commit is written: see the remarks of the port.
        queued = await QueueWakeUpsAsync(commit, wakeUps, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }
      catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange)
      {
        Forget(commit, stored, sessions, flagTotals, branch);

        throw new ConflictException(CommitErrors.ConcurrentSave, ConcurrentSaveMessage, exception);
      }
      catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, BranchConfiguration.PrimaryKeyName))
      {
        // A push created the branch meanwhile: the finalization, run again, reads its row.
        Forget(commit, stored, sessions, flagTotals, branch);

        throw new ConflictException(CommitErrors.ConcurrentSave, ConcurrentSaveMessage, exception);
      }
      catch (Exception exception) when (IsDeadlock(exception))
      {
        // Two finalizations of one branch, or of a commit and its ancestor, took the same rows in another order. The
        // engine rolled this one back: it runs again shortly, without using an attempt of the job.
        Forget(commit, stored, sessions, flagTotals, branch);

        throw new RetryLaterException(DeadlockRetryDelay, $"The finalization of commit {commitId} was chosen as the victim of a deadlock.");
      }
      catch
      {
        Forget(commit, stored, sessions, flagTotals, branch);

        throw;
      }
    }

    foreach (var jobId in queued)
    {
      wakeUpSignal.Notify(jobId);
    }
  }

  /// <summary>Tells whether a failure is the rollback of the victim of a deadlock.</summary>
  /// <param name="exception">The failure, or the update that wraps it.</param>
  /// <returns><see langword="true" /> when the engine chose the statement as the victim of a deadlock.</returns>
  private bool IsDeadlock(Exception exception) =>
    (exception as DbException ?? exception.InnerException as DbException) is { } database && jobQueue.IsDeadlock(database);

  /// <summary>Queues the finalizations a finalization wakes, in its transaction.</summary>
  /// <param name="commit">The commit, whose row is already written in the transaction.</param>
  /// <param name="wakeUps">What to wake.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The identifiers of the queued jobs.</returns>
  /// <remarks>
  ///   <para>
  ///     The neutral update locks the rows of the pending ancestors until the end of the transaction: an ancestor whose
  ///     finalization is being saved is waited for, then read ended; one saved later waits for this transaction, then
  ///     lists this commit, already <see cref="CarryforwardStatus.AncestorPending" />.
  ///   </para>
  ///   <para>
  ///     The ancestors are locked one at a time, the nearest first, as the chain gives them. Every transaction then
  ///     takes the rows of a chain in the same order, its own row first: one statement for all of them would lock
  ///     them in the order of its plan, and a commit holding a farther ancestor while it waits for a closer one that
  ///     holds its own row and waits for that farther ancestor would deadlock.
  ///   </para>
  /// </remarks>
  private async Task<IReadOnlyList<long>> QueueWakeUpsAsync(Commit commit, FinalizationWakeUps wakeUps, CancellationToken cancellationToken)
  {
    var commitId = commit.Id;
    var repositoryId = commit.RepositoryId;
    var jobs = new List<JobRequest>();

    if (wakeUps.PendingAncestorIds.Count > 0)
    {
      var seen = new HashSet<long>();
      long[] ancestors = [.. wakeUps.PendingAncestorIds.Where(seen.Add)];

      foreach (var ancestorId in ancestors)
      {
        await context.Commits
                     .Where(c => c.Id == ancestorId)
                     .ExecuteUpdateAsync(static s => s.SetProperty(static c => c.UpdatedAt, static c => c.UpdatedAt), cancellationToken)
                     .ConfigureAwait(false);
      }

      var stillPending = await context.Commits
                                      .AsNoTracking()
                                      .CountAsync(c => ancestors.Contains(c.Id) && c.State == CommitState.Pending, cancellationToken)
                                      .ConfigureAwait(false);

      if (stillPending < ancestors.Length)
      {
        jobs.Add(FinalizeCommitJobs.Create(commitId));
      }
    }

    if (wakeUps.Completed)
    {
      var waiting = await context.Commits
                                 .AsNoTracking()
                                 .Where(c => c.RepositoryId == repositoryId && c.Id != commitId && c.CarryforwardStatus == CarryforwardStatus.AncestorPending)
                                 .OrderByDescending(static c => c.Id)
                                 .Take(MaxWokenCommits)
                                 .Select(static c => c.Id)
                                 .ToListAsync(cancellationToken)
                                 .ConfigureAwait(false);

      jobs.AddRange(waiting.Select(FinalizeCommitJobs.Create));
    }

    if (wakeUps.Notification is { } notification)
    {
      jobs.Add(notification);
    }

    jobs.AddRange(wakeUps.DependentNotifications);

    var queued = new List<long>(jobs.Count);

    foreach (var job in jobs)
    {
      queued.Add(await jobQueue.EnqueueInTransactionAsync(context, job, cancellationToken).ConfigureAwait(false));
    }

    return queued;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<Session>> ListSessionsAsync(long commitId, CancellationToken cancellationToken) =>
    await context.CommitSessions
                 .AsNoTracking()
                 .Where(s => s.CommitId == commitId)
                 .OrderBy(static s => s.Number)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<IReadOnlyList<CommitFlagTotals>> ListFlagTotalsAsync(long commitId, CancellationToken cancellationToken) =>
    await context.CommitFlagTotals
                 .AsNoTracking()
                 .Where(t => t.CommitId == commitId)
                 .OrderBy(static t => t.Flag)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<IReadOnlyList<HistoryEntry>> ListHistoryAsync(
    long repositoryId,
    string branch,
    IReadOnlyCollection<string> flags,
    DateTimeOffset from,
    DateTimeOffset to,
    int limit,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(branch);
    ArgumentNullException.ThrowIfNull(flags);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

    if (flags.Count == 0)
    {
      return [];
    }

    var start = from.ToUniversalTime();
    var end = to.ToUniversalTime();

    var matching = context.CommitFlagTotals
                          .AsNoTracking()
                          .Where(t => t.RepositoryId == repositoryId && t.Branch == branch && flags.Contains(t.Flag) && t.CompletedAt >= start && t.CompletedAt <= end);

    // The cap keeps whole commits, the most recently finalized, as long as their rows fit (at least one commit): the
    // flags, which the two engines sort differently, never decide which rows are kept. A commit has one row at least,
    // so the limit bounds the commits read too.
    var commits = await matching.GroupBy(static t => new { t.CommitId, t.CompletedAt })
                                .Select(static g => new { g.Key.CommitId, g.Key.CompletedAt, Rows = g.Count() })
                                .OrderByDescending(static c => c.CompletedAt)
                                .ThenByDescending(static c => c.CommitId)
                                .Take(limit)
                                .ToListAsync(cancellationToken)
                                .ConfigureAwait(false);

    if (commits.Count == 0)
    {
      return [];
    }

    var last = commits[0];
    var rowCount = last.Rows;

    foreach (var commit in commits.Skip(1))
    {
      if (rowCount + commit.Rows > limit)
      {
        break;
      }

      rowCount += commit.Rows;
      last = commit;
    }

    // The kept commits are the first ones in the order (CompletedAt, CommitId) descending: they are read by a range up
    // to the last one kept, never by a list of identifiers (2 100 parameters at most on SQL Server).
    var lastCompletedAt = last.CompletedAt;
    var lastCommitId = last.CommitId;
    var kept = matching.Where(t => t.CompletedAt > lastCompletedAt || (t.CompletedAt == lastCompletedAt && t.CommitId >= lastCommitId));

    // The commit read from the forge may be missing: its committer date then falls back on the finalization.
    var rows = await (from t in kept
                      join c in context.Commits.AsNoTracking() on t.CommitId equals c.Id
                      join g in context.GitCommits.AsNoTracking() on new { c.RepositoryId, c.Sha } equals new { g.RepositoryId, g.Sha } into read
                      from g in read.DefaultIfEmpty()
                      select new
                      {
                        t.CommitId,
                        c.Sha,
                        t.Flag,
                        t.CompletedAt,
                        CommittedAt = g == null ? (DateTimeOffset?)null : g.CommittedAt,
                        t.Lines,
                        t.Hits,
                        t.Misses,
                        t.Partials,
                        t.Coverage,
                      })
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false);

    // Sorted in C#, the same on both engines: the most recent commit first, then the flags by code point.
    return
    [
      .. rows.OrderByDescending(static r => r.CompletedAt)
             .ThenByDescending(static r => r.CommitId)
             .ThenBy(static r => r.Flag, StringComparer.Ordinal)
             .Select(static r => new HistoryEntry(r.CommitId, r.Sha, r.Flag, r.CompletedAt, r.CommittedAt, r.Lines, r.Hits, r.Misses, r.Partials, r.Coverage)),
    ];
  }

  /// <inheritdoc />
  public async Task<byte[]?> GetReportAsync(long commitId, CancellationToken cancellationToken) =>
    await context.CommitReports
                 .AsNoTracking()
                 .Where(r => r.CommitId == commitId)
                 .Select(static r => r.Data)
                 .SingleOrDefaultAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<byte[]?> GetFileIndexAsync(long commitId, CancellationToken cancellationToken) =>
    await context.CommitReports
                 .AsNoTracking()
                 .Where(r => r.CommitId == commitId)
                 .Select(static r => r.FileIndex)
                 .SingleOrDefaultAsync(cancellationToken)
                 .ConfigureAwait(false);

  #endregion

  #region Methods

  /// <summary>Takes an ended upload back to its received state, if nobody changed it since it was read.</summary>
  /// <param name="requeue">The upload and the log it starts again from.</param>
  /// <param name="cancellationToken">A token to cancel the update.</param>
  /// <returns>The upload of this scope, now received, or <see langword="null" /> when it was not taken.</returns>
  /// <remarks>
  ///   <para>
  ///     The row is locked by an update that only matches the state the upload was read in, and stays locked
  ///     until the transaction ends: of two requests that read the same ended upload, the second one waits,
  ///     then finds it received and leaves it.
  ///   </para>
  ///   <para>
  ///     The update goes through the filter of the organization, and the change is then made on the row
  ///     tracked by this scope, never on an upload read elsewhere: the write guard knows its organization.
  ///   </para>
  /// </remarks>
  private async Task<Upload?> TryTakeAsync(UploadRequeue requeue, CancellationToken cancellationToken)
  {
    var id = requeue.Upload.Id;
    var read = requeue.Upload.State;

    if (read is not (UploadState.Processed or UploadState.Failed))
    {
      return null;
    }

    var locked = await context.Uploads
                              .Where(u => u.Id == id && u.State == read)
                              .ExecuteUpdateAsync(static setters => setters.SetProperty(static u => u.State, static u => u.State), cancellationToken)
                              .ConfigureAwait(false);

    if (locked == 0)
    {
      return null;
    }

    var upload = await context.Uploads.SingleAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);

    // A row this scope already tracked may be older than the one just locked: the lock proved its state.
    await context.Entry(upload).ReloadAsync(cancellationToken).ConfigureAwait(false);
    upload.Requeue(requeue.StartingLog);

    return upload;
  }

  /// <summary>Applies the coverage of an upload to its row.</summary>
  /// <param name="uploadId">The identifier of the upload.</param>
  /// <param name="stored">The row of a previous attempt, or <see langword="null" />.</param>
  /// <param name="blob">The blob of the upload, or <see langword="null" /> when it has none.</param>
  /// <returns>The row touched by the change, or <see langword="null" /> when there is none.</returns>
  private UploadCoverageRecord? WriteCoverage(long uploadId, UploadCoverageRecord? stored, byte[]? blob)
  {
    if (blob is null)
    {
      if (stored is not null)
      {
        context.UploadCoverages.Remove(stored);
      }

      return stored;
    }

    if (stored is null)
    {
      var added = new UploadCoverageRecord { UploadId = uploadId, Data = blob };
      context.UploadCoverages.Add(added);

      return added;
    }

    stored.Codec = UploadCoverageRecord.CurrentCodec;
    stored.Data = blob;

    return stored;
  }

  /// <summary>Applies the report of a finalization to its row.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="stored">The row of a previous finalization, or <see langword="null" />.</param>
  /// <param name="report">The blob of the merged report.</param>
  /// <param name="fileIndex">The blob of the counters of its files.</param>
  /// <returns>The row of the report.</returns>
  private CommitReportRecord WriteReport(Commit commit, CommitReportRecord? stored, byte[] report, byte[] fileIndex)
  {
    if (stored is null)
    {
      stored = new CommitReportRecord { CommitId = commit.Id };
      context.CommitReports.Add(stored);
    }

    stored.Revision = commit.ReportRevision;
    stored.Codec = UploadCoverageRecord.CurrentCodec;
    stored.Data = report;
    stored.FileIndex = fileIndex;

    return stored;
  }

  /// <summary>Forgets what a failed finalization left in the context.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="report">The row of the report, or <see langword="null" />.</param>
  /// <param name="sessions">The sessions.</param>
  /// <param name="flagTotals">The totals.</param>
  /// <param name="branch">The branch, or <see langword="null" />.</param>
  private void Forget(
    Commit commit,
    CommitReportRecord? report,
    IReadOnlyList<Session> sessions,
    IReadOnlyList<CommitFlagTotals> flagTotals,
    Branch? branch)
  {
    context.Entry(commit).State = EntityState.Detached;

    if (report is not null)
    {
      context.Entry(report).State = EntityState.Detached;
    }

    if (branch is not null)
    {
      context.Entry(branch).State = EntityState.Detached;
    }

    foreach (var session in sessions)
    {
      context.Entry(session).State = EntityState.Detached;
    }

    foreach (var total in flagTotals)
    {
      context.Entry(total).State = EntityState.Detached;
    }
  }

  #endregion
}
