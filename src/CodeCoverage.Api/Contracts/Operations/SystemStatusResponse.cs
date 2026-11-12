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
/// <param name="WorkerLastSeenAt">When a worker loop of the answering process last turned, or <see langword="null" />.</param>
/// <param name="GitHubRemaining">The requests left on the GitHub quota, by installation.</param>
public sealed record SystemStatusResponse(
  int Queued,
  int Running,
  int Failed,
  int Dead,
  double? OldestQueuedAgeSeconds,
  DateTimeOffset? WorkerLastSeenAt,
  IReadOnlyDictionary<string, long> GitHubRemaining);
