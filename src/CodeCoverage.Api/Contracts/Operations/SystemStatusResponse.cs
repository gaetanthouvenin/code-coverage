// ------------------------------------------------------------------------------------------------
// <copyright file="SystemStatusResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Operations;

/// <summary>The state of the job queue, of the worker and of the GitHub quota.</summary>
/// <param name="Queued">The number of jobs waiting.</param>
/// <param name="Running">The number of jobs running.</param>
/// <param name="Failed">The number of jobs that failed for good.</param>
/// <param name="Dead">The number of jobs that used up their attempts.</param>
/// <param name="OldestQueuedAgeSeconds">How long the oldest job due has been waiting, in seconds, or <see langword="null" />.</param>
/// <param name="WorkerLastSeenAt">The latest beat of any worker, or <see langword="null" /> when none was written.</param>
/// <param name="GitHubRemaining">The requests left on the GitHub quota, by installation.</param>
/// <param name="Workers">The heartbeat of each worker process of the last 24 hours, the most recent first.</param>
/// <param name="GitHubQuotas">The last GitHub quota of each installation, with its limit and its reset.</param>
/// <remarks>Fields only added since M3.13a: a reader of the former answer keeps working.</remarks>
public sealed record SystemStatusResponse(
  int Queued,
  int Running,
  int Failed,
  int Dead,
  double? OldestQueuedAgeSeconds,
  DateTimeOffset? WorkerLastSeenAt,
  IReadOnlyDictionary<string, long> GitHubRemaining,
  IReadOnlyList<WorkerHeartbeatModel> Workers,
  IReadOnlyList<GitHubQuotaModel> GitHubQuotas);
