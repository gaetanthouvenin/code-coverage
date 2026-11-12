// ------------------------------------------------------------------------------------------------
// <copyright file="SystemStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Operations;

/// <summary>The state of the job queue, of the workers and of the GitHub quota: metadata only.</summary>
/// <param name="Queued">The number of jobs waiting.</param>
/// <param name="Running">The number of jobs running.</param>
/// <param name="Failed">The number of jobs that failed for good.</param>
/// <param name="Dead">The number of jobs that used up their attempts.</param>
/// <param name="OldestQueuedAge">How long the oldest job due has been waiting, or <see langword="null" /> when none is.</param>
/// <param name="WorkerLastSeenAt">
///   When a worker loop of this process last turned, in UTC, or <see langword="null" /> when no worker runs here.
/// </param>
/// <param name="GitHubRemaining">The requests left on the GitHub quota, by installation, as last read in this process.</param>
/// <remarks>
///   A read model for the administrators. The heartbeat and the quota are those of the process that answers: one Api
///   replica runs the worker and calls GitHub, a documented limit (ARCHITECTURE §12.6).
/// </remarks>
public sealed record SystemStatus(
  int Queued,
  int Running,
  int Failed,
  int Dead,
  TimeSpan? OldestQueuedAge,
  DateTimeOffset? WorkerLastSeenAt,
  IReadOnlyDictionary<string, long> GitHubRemaining);
