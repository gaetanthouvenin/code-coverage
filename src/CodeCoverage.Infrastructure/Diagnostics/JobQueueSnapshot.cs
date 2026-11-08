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
internal sealed record JobQueueSnapshot(IReadOnlyDictionary<string, long> QueuedByType, double OldestDueAgeSeconds);
