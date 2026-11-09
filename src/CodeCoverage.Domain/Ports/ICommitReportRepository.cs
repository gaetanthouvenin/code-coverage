// ------------------------------------------------------------------------------------------------
// <copyright file="ICommitReportRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;

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

  /// <summary>Saves a finalization, in one transaction: the commit, its report, its sessions and its flag totals.</summary>
  /// <param name="commit">The commit, read in this scope and carrying its <see cref="Commit.RecordFinalization" />.</param>
  /// <param name="report">The blob of the merged report.</param>
  /// <param name="fileIndex">The blob of the counters of its files.</param>
  /// <param name="sessions">The sessions of the commit, which replace the previous ones.</param>
  /// <param name="flagTotals">The totals of the commit, which replace the previous ones.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the transaction is committed.</returns>
  Task SaveFinalizedAsync(
    Commit commit,
    byte[] report,
    byte[] fileIndex,
    IReadOnlyList<Session> sessions,
    IReadOnlyList<CommitFlagTotals> flagTotals,
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
