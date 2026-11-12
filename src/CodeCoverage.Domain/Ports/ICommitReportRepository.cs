// ------------------------------------------------------------------------------------------------
// <copyright file="ICommitReportRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Ports;

/// <summary>The results of the processing of the uploads and of the finalization of the commits.</summary>
/// <remarks>The blobs are written and read as they are: <see cref="ICoverageReportCodec" /> encodes and decodes them.</remarks>
public interface ICommitReportRepository
{
  #region Methods

  /// <summary>Gets the blob of a processed upload.</summary>
  /// <param name="uploadId">The identifier of the upload.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The blob, or <see langword="null" /> when the upload has none or cannot be read.</returns>
  Task<byte[]?> GetUploadCoverageAsync(long uploadId, CancellationToken cancellationToken);

  /// <summary>Adds an upload just received and queues its job, in one transaction.</summary>
  /// <param name="upload">The upload.</param>
  /// <param name="job">The job to queue.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns><see langword="true" /> when it was added; <see langword="false" /> when its idempotency key is already used on the commit.</returns>
  /// <remarks>Refused as a conflict (<c>General.ConcurrentChange</c>) when the commit is not one of the organization of the scope.</remarks>
  Task<bool> TryAddReceivedUploadAsync(Upload upload, JobRequest job, CancellationToken cancellationToken);

  /// <summary>Saves the result of the processing of an upload and queues its job, in one transaction.</summary>
  /// <param name="upload">The upload, read in this scope.</param>
  /// <param name="blob">The blob of the upload, or <see langword="null" /> when it failed and has none.</param>
  /// <param name="job">The job to queue.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the transaction is committed.</returns>
  Task SaveProcessedUploadAsync(Upload upload, byte[]? blob, JobRequest job, CancellationToken cancellationToken);

  /// <summary>Puts ended uploads back to their received state and queues the processing of each, in one transaction.</summary>
  /// <param name="requeues">The uploads, as they were read, and the log each one starts again from.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The uploads that were taken, now received; empty when none was.</returns>
  /// <remarks>
  ///   <para>
  ///     The transition is guarded: an upload is taken only while it is still in the state it was read in,
  ///     processed or failed. An upload that is received or being processed already has its job, and one
  ///     that another request took in the meantime is left to it: neither is queued twice.
  ///   </para>
  ///   <para>
  ///     The blob of an upload is kept until its next processing replaces or removes it; a finalization
  ///     never reads it meanwhile, since it stops while an upload of the commit is not ended.
  ///   </para>
  /// </remarks>
  Task<IReadOnlyList<Upload>> RequeueUploadsAsync(IReadOnlyList<UploadRequeue> requeues, CancellationToken cancellationToken);

  /// <summary>
  ///   Saves a finalization, in one transaction: the commit, its report, its sessions, its flag totals, the covered
  ///   head of its branch and the finalizations it wakes.
  /// </summary>
  /// <param name="commit">The commit, read in this scope and carrying its <see cref="Commit.RecordFinalization" />.</param>
  /// <param name="report">The blob of the merged report.</param>
  /// <param name="fileIndex">The blob of the counters of its files.</param>
  /// <param name="sessions">The sessions of the commit, which replace the previous ones.</param>
  /// <param name="flagTotals">The totals of the commit, which replace the previous ones.</param>
  /// <param name="branch">
  ///   The branch of the commit with its covered head recorded (<see cref="Branch.Cover" />): an instance read in this
  ///   scope, or a new one to add. <see langword="null" /> when the commit names no branch.
  /// </param>
  /// <param name="wakeUps">The finalizations to queue in the same transaction.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the transaction is committed.</returns>
  /// <remarks>
  ///   <para>
  ///     The branch is written with the commit: a push that moved or created it meanwhile makes the save a conflict
  ///     (<c>Commit.ConcurrentSave</c>), and the finalization, run again, reads the head the push left.
  ///   </para>
  ///   <para>
  ///     A commit that completes queues the finalization of the <see cref="CarryforwardStatus.AncestorPending" />
  ///     commits of its repository, 10 at most, read after its own row is written. Their ancestry is not known here: a
  ///     finalization is deterministic, so running one that did not depend on the commit gives the same report again.
  ///   </para>
  ///   <para>
  ///     A commit that passed pending ancestors locks their rows, reads their state, and queues its own finalization
  ///     when one of them has ended. Whatever the order of the two transactions, either the ancestor lists the waiting
  ///     commit, or the waiting commit sees the ancestor ended: no wake-up is lost.
  ///   </para>
  /// </remarks>
  Task SaveFinalizedAsync(
    Commit commit,
    byte[] report,
    byte[] fileIndex,
    IReadOnlyList<Session> sessions,
    IReadOnlyList<CommitFlagTotals> flagTotals,
    Branch? branch,
    FinalizationWakeUps wakeUps,
    CancellationToken cancellationToken);

  /// <summary>Lists the sessions of a commit, for reading only.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The sessions, by number.</returns>
  Task<IReadOnlyList<Session>> ListSessionsAsync(long commitId, CancellationToken cancellationToken);

  /// <summary>Lists the totals of the flags of a commit, by flag, for reading only.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The totals, the one without flag (<c>''</c>) included when the commit is finalized.</returns>
  /// <remarks>Kept forever, the purge included: the detail of a commit, the badges and the history read them.</remarks>
  Task<IReadOnlyList<CommitFlagTotals>> ListFlagTotalsAsync(long commitId, CancellationToken cancellationToken);

  /// <summary>Lists the totals of flags of the finalized commits of a branch, for its history.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="branch">The branch.</param>
  /// <param name="flags">The flags, case-sensitive; the empty text is the whole project.</param>
  /// <param name="from">The first finalization date read.</param>
  /// <param name="to">The last finalization date read.</param>
  /// <param name="limit">
  ///   The number of totals read at most, cut on whole commits (the most recently finalized kept while their rows fit),
  ///   so that the result does not depend on how an engine sorts the flags; at least one commit is read.
  /// </param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The totals, each with the SHA of its commit and the date of its committer when the commit was read from the forge,
  ///   the most recently finalized commit first, then its flags by code point.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="branch" /> or <paramref name="flags" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit" /> is zero or negative.</exception>
  Task<IReadOnlyList<HistoryEntry>> ListHistoryAsync(
    long repositoryId,
    string branch,
    IReadOnlyCollection<string> flags,
    DateTimeOffset from,
    DateTimeOffset to,
    int limit,
    CancellationToken cancellationToken);

  /// <summary>Gets the blob of the merged report of a commit.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The blob, or <see langword="null" /> when the commit has none or cannot be read.</returns>
  Task<byte[]?> GetReportAsync(long commitId, CancellationToken cancellationToken);

  /// <summary>Gets the blob of the counters of the files of a commit, without reading its lines.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The blob, or <see langword="null" /> when the commit has none or cannot be read.</returns>
  Task<byte[]?> GetFileIndexAsync(long commitId, CancellationToken cancellationToken);

  #endregion
}
