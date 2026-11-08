// ------------------------------------------------------------------------------------------------
// <copyright file="IJobMonitor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Domain.Ports;

/// <summary>Reads the job queue for the administrators (<c>/admin/operations</c>, <c>/api/v1/ops/jobs</c>).</summary>
/// <remarks>Read only: the jobs are queued through <see cref="IJobQueue" /> or the repositories.</remarks>
public interface IJobMonitor
{
  #region Methods

  /// <summary>Finds a job.</summary>
  /// <param name="id">The identifier of the job.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The job, or <see langword="null" /> when it does not exist (or was purged).</returns>
  Task<JobSummary?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Lists the jobs, the most recent first.</summary>
  /// <param name="state">The state of the jobs to list, or <see langword="null" /> for every state.</param>
  /// <param name="skip">How many jobs to skip.</param>
  /// <param name="take">How many jobs to return at most.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The jobs, by identifier, descending.</returns>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="skip" /> is negative or <paramref name="take" /> is not positive.
  /// </exception>
  Task<IReadOnlyList<JobSummary>> ListAsync(JobState? state, int skip, int take, CancellationToken cancellationToken);

  #endregion
}
