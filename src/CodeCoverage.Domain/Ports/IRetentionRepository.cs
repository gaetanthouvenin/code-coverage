// ------------------------------------------------------------------------------------------------
// <copyright file="IRetentionRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>Deletes what the retention of the instance no longer keeps (ARCHITECTURE §4.4), in batches.</summary>
/// <remarks>
///   <para>
///     The methods of an organization work in the organization of the scope, under its query filter; those of the
///     instance touch the operational tables only (<c>Jobs</c>, <c>WebhookDeliveries</c>). Each method deletes in batches
///     of the given size until nothing is left, and returns how many rows or commits it handled.
///   </para>
///   <para>
///     The commits and their flag totals are kept forever (history, badges); a compacted commit only loses its reports.
///   </para>
/// </remarks>
public interface IRetentionRepository
{
  #region Methods

  /// <summary>Deletes the raw files of the uploads whose time is over, and their chunks.</summary>
  /// <param name="now">The current time: the files whose <c>ExpiresAt</c> is past are deleted.</param>
  /// <param name="batchSize">The number of files of a batch.</param>
  /// <param name="cancellationToken">A token to cancel the deletion.</param>
  /// <returns>The number of files deleted.</returns>
  Task<int> PurgeRawUploadsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

  /// <summary>
  ///   Compacts the finalized commits off the default branch completed before a date: their merged report, the reports of
  ///   their uploads and the data of their sessions are deleted, and they pass to <c>Compacted</c>.
  /// </summary>
  /// <param name="before">The date before which a commit is compacted.</param>
  /// <param name="batchSize">The number of commits of a batch.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The number of commits compacted.</returns>
  /// <remarks>
  ///   Kept: the commits of the default branch, the commits without a branch, the heads of the branches, the SHAs of the
  ///   open pull requests (head, base, fork point, compared base), the commits a session of another commit carries forward,
  ///   and the commits that still receive an upload.
  /// </remarks>
  Task<int> CompactCommitsAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken);

  /// <summary>Deletes the diffs and the trees read from the forge before a date: they are read again when needed.</summary>
  /// <param name="before">The date before which a cached entry is deleted.</param>
  /// <param name="batchSize">The number of entries of a batch.</param>
  /// <param name="cancellationToken">A token to cancel the deletion.</param>
  /// <returns>The number of entries deleted.</returns>
  Task<int> PurgeForgeCachesAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken);

  /// <summary>Deletes the jobs that ended (succeeded, failed or dead) before a date; never a queued or running one.</summary>
  /// <param name="before">The date before which an ended job is deleted.</param>
  /// <param name="batchSize">The number of jobs of a batch.</param>
  /// <param name="cancellationToken">A token to cancel the deletion.</param>
  /// <returns>The number of jobs deleted.</returns>
  Task<int> PurgeJobsAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken);

  /// <summary>Deletes the webhook deliveries received before a date.</summary>
  /// <param name="before">The date before which a delivery is deleted.</param>
  /// <param name="batchSize">The number of deliveries of a batch.</param>
  /// <param name="cancellationToken">A token to cancel the deletion.</param>
  /// <returns>The number of deliveries deleted.</returns>
  Task<int> PurgeWebhookDeliveriesAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken);

  #endregion
}
