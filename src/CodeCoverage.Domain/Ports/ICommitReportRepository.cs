// ------------------------------------------------------------------------------------------------
// <copyright file="ICommitReportRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;

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
  ///   Saves a finalization, in one transaction: the commit, its report, its sessions, its flag totals and the
  ///   finalizations it wakes.
  /// </summary>
  /// <param name="commit">The commit, read in this scope and carrying its <see cref="Commit.RecordFinalization" />.</param>
  /// <param name="report">The blob of the merged report.</param>
  /// <param name="fileIndex">The blob of the counters of its files.</param>
  /// <param name="sessions">The sessions of the commit, which replace the previous ones.</param>
  /// <param name="flagTotals">The totals of the commit, which replace the previous ones.</param>
  /// <param name="wakeUps">The finalizations to queue in the same transaction.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the transaction is committed.</returns>
  /// <remarks>
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
    FinalizationWakeUps wakeUps,
    CancellationToken cancellationToken);

  /// <summary>Lists the sessions of a commit, for reading only.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The sessions, by number.</returns>
  Task<IReadOnlyList<Session>> ListSessionsAsync(long commitId, CancellationToken cancellationToken);

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
