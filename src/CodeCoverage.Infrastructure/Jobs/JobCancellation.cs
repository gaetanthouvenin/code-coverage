// ------------------------------------------------------------------------------------------------
// <copyright file="JobCancellation.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>Why the <see cref="JobWorker" /> canceled a running job: each cause has its own outcome.</summary>
internal enum JobCancellation
{
  /// <summary>The job was not canceled.</summary>
  None,

  /// <summary>The worker no longer holds the job: nothing is written.</summary>
  LeaseLost,

  /// <summary>The host stops: the job is queued again at once, without consuming its attempt.</summary>
  HostStopping,

  /// <summary>The job ran longer than <see cref="JobWorker.JobTimeout" />: it is retried after a backoff.</summary>
  TimedOut,
}
