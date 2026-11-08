// ------------------------------------------------------------------------------------------------
// <copyright file="JobState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Operations;

/// <summary>Where a background job stands.</summary>
/// <remarks>Stored as text: a member is never renamed once released.</remarks>
public enum JobState
{
  /// <summary>Waiting for a worker, from its <c>RunAfter</c> time on.</summary>
  Queued,

  /// <summary>Claimed by a worker, which holds its lease.</summary>
  Running,

  /// <summary>Done, or absorbed by a queued twin.</summary>
  Succeeded,

  /// <summary>Refused for good (not found, invalid): never retried automatically.</summary>
  Failed,

  /// <summary>Failed on each of its attempts: waits for a manual retry.</summary>
  Dead,
}
