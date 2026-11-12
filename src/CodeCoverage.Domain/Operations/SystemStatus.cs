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
/// <param name="WorkerLastSeenAt">The latest beat of any worker, in UTC, or <see langword="null" /> when none was ever written.</param>
/// <param name="GitHubRemaining">The requests left on the GitHub quota, by installation, as last written.</param>
/// <param name="Workers">The heartbeat of each worker process of the last 24 hours, the most recent first.</param>
/// <param name="GitHubQuotas">The last GitHub quota of each installation, with its limit and its reset, by installation.</param>
/// <remarks>
///   A read model for the administrators. The heartbeats and the quotas are read from the operational tables the worker
///   of the Api writes every 15 seconds (M3.13a): any process, the Web included, reads the same values.
/// </remarks>
public sealed record SystemStatus(
  int Queued,
  int Running,
  int Failed,
  int Dead,
  TimeSpan? OldestQueuedAge,
  DateTimeOffset? WorkerLastSeenAt,
  IReadOnlyDictionary<string, long> GitHubRemaining,
  IReadOnlyList<WorkerHeartbeat> Workers,
  IReadOnlyList<GitHubQuota> GitHubQuotas);
