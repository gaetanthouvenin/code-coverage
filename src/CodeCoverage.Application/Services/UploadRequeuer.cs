// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRequeuer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>What the retry of an upload and the reprocessing of a commit share: the raw files must still be kept, and an ended upload goes back in the queue.</summary>
/// <param name="reports">The repository that puts uploads back in the queue.</param>
/// <param name="rawStore">The store of the raw files.</param>
internal sealed class UploadRequeuer(ICommitReportRepository reports, IRawUploadStore rawStore)
{
  #region Methods

  /// <summary>Tells whether an upload ended, so that it can be processed again.</summary>
  /// <param name="upload">The upload.</param>
  /// <returns><see langword="true" /> when it is processed or failed; an upload received or being processed still has its job.</returns>
  public static bool HasEnded(Upload upload)
  {
    ArgumentNullException.ThrowIfNull(upload);

    return upload.State is UploadState.Processed or UploadState.Failed;
  }

  /// <summary>Builds the log an upload starts again from.</summary>
  /// <param name="upload">The upload.</param>
  /// <returns>
  ///   The messages that tell its move off a synthetic merge commit: the move is not done again, so
  ///   nothing else would report it. Everything the previous processing wrote is dropped.
  /// </returns>
  public static ProcessingLog StartingLog(Upload upload)
  {
    ArgumentNullException.ThrowIfNull(upload);

    return upload.ProcessingLog.Messages
                 .Where(static message => message.StartsWith(UploadErrors.MergeCommitRewritten, StringComparison.Ordinal))
                 .Aggregate(ProcessingLog.Empty, static (log, message) => log.Add(message));
  }

  /// <summary>Tells whether the raw reports of one of the uploads are no longer kept.</summary>
  /// <param name="uploads">The uploads to process again.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns><see langword="true" /> when one upload at least can no longer be parsed again.</returns>
  public async Task<bool> HasExpiredRawAsync(IReadOnlyList<Upload> uploads, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(uploads);

    var expired = await rawStore.ListExpiredAsync([.. uploads.Select(static upload => upload.PublicId)], cancellationToken).ConfigureAwait(false);

    return expired.Count > 0;
  }

  /// <summary>Puts ended uploads back in the queue, in one transaction.</summary>
  /// <param name="uploads">The uploads, as they were read.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The uploads that were taken: one that changed since it was read is left alone.</returns>
  public Task<IReadOnlyList<Upload>> RequeueAsync(IReadOnlyList<Upload> uploads, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(uploads);

    return reports.RequeueUploadsAsync([.. uploads.Select(static upload => new UploadRequeue(upload, StartingLog(upload)))], cancellationToken);
  }

  #endregion
}
