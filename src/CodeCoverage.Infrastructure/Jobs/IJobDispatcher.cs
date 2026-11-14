// ------------------------------------------------------------------------------------------------
// <copyright file="IJobDispatcher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>Runs a claimed job: turns it into the command of its type and sends it.</summary>
/// <remarks>
///   Implemented by <see cref="JobDispatcher" />. Resolved by the <see cref="JobWorker" /> in the scope of
///   each job, which already runs as the system principal.
/// </remarks>
internal interface IJobDispatcher
{
  #region Methods

  /// <summary>Runs a job.</summary>
  /// <param name="job">The job, claimed by the worker.</param>
  /// <param name="cancellationToken">
  ///   A token canceled when the job times out, when the worker loses its lease or when the host stops.
  /// </param>
  /// <returns>A task that completes when the command of the job was handled.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="job" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidJobException">The type of the job is unknown or its payload is invalid.</exception>
  Task DispatchAsync(JobRecord job, CancellationToken cancellationToken);

  /// <summary>Releases what a job holds once it ended for good: it failed for good, or used all its attempts.</summary>
  /// <param name="job">The job, which will not run again unless an operator retries it.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when what the job held is released; most types hold nothing.</returns>
  /// <remarks>
  ///   <c>ImportCodecovHistory</c> deletes its credential (<c>AbandonCodecovImport</c>): a token is never kept for a job
  ///   that will not run again.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="job" /> is <see langword="null" />.</exception>
  Task AbandonAsync(JobRecord job, CancellationToken cancellationToken);

  #endregion
}
