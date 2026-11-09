// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReportRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
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

  /// <summary>The message of the refusal of a finalization that lost a race; it is sent to the caller.</summary>
  private const string ConcurrentSaveMessage = "The commit was finalized in the meantime; finalize it again.";

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
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(report);
    ArgumentNullException.ThrowIfNull(fileIndex);
    ArgumentNullException.ThrowIfNull(sessions);
    ArgumentNullException.ThrowIfNull(flagTotals);

    InvalidOperationException.ThrowIf(!context.Entry(commit).Property(static c => c.Version).IsModified, NotFinalizedMessage);
    ArgumentException.ThrowIf(sessions.Any(s => s.CommitId != commit.Id), ForeignCommitMessage, nameof(sessions));
    ArgumentException.ThrowIf(flagTotals.Any(t => t.CommitId != commit.Id), ForeignCommitMessage, nameof(flagTotals));

    var commitId = commit.Id;
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

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }
      catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange)
      {
        Forget(commit, stored, sessions, flagTotals);

        throw new ConflictException(CommitErrors.ConcurrentSave, ConcurrentSaveMessage, exception);
      }
      catch
      {
        Forget(commit, stored, sessions, flagTotals);

        throw;
      }
    }
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
  private void Forget(Commit commit, CommitReportRecord? report, IReadOnlyList<Session> sessions, IReadOnlyList<CommitFlagTotals> flagTotals)
  {
    context.Entry(commit).State = EntityState.Detached;

    if (report is not null)
    {
      context.Entry(report).State = EntityState.Detached;
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
