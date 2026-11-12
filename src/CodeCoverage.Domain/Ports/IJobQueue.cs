// ------------------------------------------------------------------------------------------------
// <copyright file="IJobQueue.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Domain.Ports;

/// <summary>The queue of the background jobs, run by the workers of the Api.</summary>
/// <remarks>
///   <para>
///     A job that follows a change is queued <b>by the repository method that saves the change</b>, in
///     its transaction, from a <see cref="JobRequest" /> parameter: the change and its job are saved
///     together or not at all. This port is kept for the isolated triggers, which change nothing else
///     (a notification forced from the Web, a manual retry).
///   </para>
///   <para>
///     The queue applies the deduplication of the request: see <see cref="JobRequest" />.
///   </para>
/// </remarks>
public interface IJobQueue
{
  #region Methods

  /// <summary>Queues a job, saved at once in a transaction of its own.</summary>
  /// <param name="request">The job to run.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>
  ///   The identifier of the queued job: a new job, or the queued twin the request was merged into.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">
  ///   The current scope does not allow the organization of the request: see
  ///   <see cref="JobRequest.ResolveOrganizationId" />.
  /// </exception>
  Task<long> EnqueueAsync(JobRequest request, CancellationToken cancellationToken);

  /// <summary>Queues a job, saved at once in a transaction of its own, and tells whether it is a new one.</summary>
  /// <param name="request">The job to run.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>
  ///   <see langword="true" /> when a new job was queued; <see langword="false" /> when the request was merged into
  ///   its queued twin, a job waiting for its retry for instance.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">
  ///   The current scope does not allow the organization of the request: see
  ///   <see cref="JobRequest.ResolveOrganizationId" />.
  /// </exception>
  Task<bool> TryEnqueueAsync(JobRequest request, CancellationToken cancellationToken);

  /// <summary>Puts a failed or dead job back in the queue, to run at once, and wakes a worker.</summary>
  /// <param name="id">The identifier of the job.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>What became of the retry.</returns>
  /// <remarks>
  ///   The row itself is reset, never copied: its attempts go back to zero, its lease is cleared and it keeps its
  ///   organization. A job that is not failed or dead, or whose twin with the same deduplication key already waits, is
  ///   left as it is.
  /// </remarks>
  Task<Operations.JobRetryResult> RetryAsync(long id, CancellationToken cancellationToken);

  #endregion
}
