// ------------------------------------------------------------------------------------------------
// <copyright file="JobQueueSnapshot.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Diagnostics;

/// <summary>The state of the job queue at one moment, as published by the gauges of <see cref="CodeCoverageMeter" />.</summary>
/// <param name="QueuedByType">The number of queued jobs, by job type; a type without queued job is absent.</param>
/// <param name="OldestDueAgeSeconds">
///   How long, in seconds, the oldest job that may run has waited for a worker since its <c>RunAfter</c>; 0 when
///   no job may run now.
/// </param>
/// <param name="Dead">The number of dead jobs, which used all their attempts and wait for an operator (M4.04).</param>
/// <param name="HeartbeatAgeSeconds">
///   The age, in seconds, of the most recent beat of <c>WorkerHeartbeats</c>, or <see langword="null" /> when no worker
///   ever beat (M4.04).
/// </param>
/// <param name="GitHubLimits">
///   The size of the GitHub quota of each installation, as <c>GitHubQuotas</c> last recorded it (M4.04); empty when none is
///   known.
/// </param>
/// <param name="GitHubRemaining">
///   The requests left on the GitHub quota of each installation whose size is known, read from the same
///   <c>GitHubQuotas</c> rows as <paramref name="GitHubLimits" />; a quota whose reset time has passed counts as full.
/// </param>
internal sealed record JobQueueSnapshot(
  IReadOnlyDictionary<string, long> QueuedByType,
  double OldestDueAgeSeconds,
  long Dead = 0,
  double? HeartbeatAgeSeconds = null,
  IReadOnlyDictionary<string, long>? GitHubLimits = null,
  IReadOnlyDictionary<string, long>? GitHubRemaining = null);
