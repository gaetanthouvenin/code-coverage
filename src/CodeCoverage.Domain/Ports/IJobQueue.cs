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

  #endregion
}
