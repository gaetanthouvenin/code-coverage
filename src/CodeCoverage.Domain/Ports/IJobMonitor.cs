// ------------------------------------------------------------------------------------------------
// <copyright file="IJobMonitor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Domain.Ports;

/// <summary>Reads the job queue for the administrators (<c>/admin/operations</c>, <c>/api/v1/ops/jobs</c>).</summary>
/// <remarks>
///   Read only, metadata only: never the payload, the keys or the text of an error. The jobs are queued, and retried,
///   through <see cref="IJobQueue" /> or the repositories.
/// </remarks>
public interface IJobMonitor
{
  #region Methods

  /// <summary>Finds a job.</summary>
  /// <param name="id">The identifier of the job.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The job, or <see langword="null" /> when it does not exist (or was purged).</returns>
  Task<JobSummary?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Lists one page of the jobs, the most recent first.</summary>
  /// <param name="state">The state of the jobs to list, or <see langword="null" /> for every state.</param>
  /// <param name="page">The number of the page, from 1.</param>
  /// <param name="pageSize">The number of jobs of a page.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The page, by identifier, descending.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="page" /> or <paramref name="pageSize" /> is not positive.</exception>
  Task<JobPage> ListAsync(JobState? state, int page, int pageSize, CancellationToken cancellationToken);

  /// <summary>Reads the state of the queue, the heartbeat of the worker and the GitHub quota left.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The status.</returns>
  Task<SystemStatus> GetStatusAsync(CancellationToken cancellationToken);

  /// <summary>Counts the jobs queued or running of each organization; the jobs of the instance are left out.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The count of each organization that has one, by identifier of the organization.</returns>
  Task<IReadOnlyDictionary<long, int>> CountQueuedByOrganizationAsync(CancellationToken cancellationToken);

  #endregion
}
