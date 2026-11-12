// ------------------------------------------------------------------------------------------------
// <copyright file="JobRetryResult.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Operations;

/// <summary>What became of the retry of a job.</summary>
public enum JobRetryResult
{
  /// <summary>The job was put back in the queue, to run at once.</summary>
  Retried,

  /// <summary>No job has this identifier, or it was purged.</summary>
  NotFound,

  /// <summary>The job is not failed or dead, or a twin with the same deduplication key already waits.</summary>
  NotRetryable,
}
