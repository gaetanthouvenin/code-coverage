// ------------------------------------------------------------------------------------------------
// <copyright file="NotifyCommitJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the notification of a commit: its statuses, then its comment.</summary>
/// <remarks>Both jobs share the lock of the finalization of the commit: a commit is never notified while its report is rebuilt.</remarks>
public static class NotifyCommitJobs
{
  #region Methods

  /// <summary>Builds the notification that follows a finalization, debounced.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="now">The current time.</param>
  /// <param name="debounce">How long the notification waits for another finalization of the commit.</param>
  /// <param name="maxDelay">How long it can be pushed back, counted from the first request.</param>
  /// <returns>The job.</returns>
  /// <remarks>
  ///   A burst of uploads finalizes the commit several times: each request pushes the waiting job back
  ///   instead of adding one, so the commit is notified once, <paramref name="maxDelay" /> after the first
  ///   request at the latest.
  /// </remarks>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="debounce" /> is negative, or <paramref name="maxDelay" /> is not positive.</exception>
  public static JobRequest Create(long commitId, DateTimeOffset now, TimeSpan debounce, TimeSpan maxDelay)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(debounce, TimeSpan.Zero);

    var id = commitId.ToString(CultureInfo.InvariantCulture);

    return JobRequest.Create(
      JobTypes.NotifyCommit,
      JsonSerializer.Serialize(new { CommitId = commitId, Force = false }),
      $"commit:{id}",
      $"notify:{id}",
      now + debounce,
      maxDelay: maxDelay
    );
  }

  /// <summary>Builds the notification asked for by hand, which runs at once and ignores <c>after_n_builds</c>.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <returns>The job, merged with the forced one already waiting.</returns>
  /// <remarks>
  ///   Its deduplication key is its own: merged with the debounced job, which keeps its payload, the
  ///   request would lose what makes it forced.
  /// </remarks>
  public static JobRequest CreateForced(long commitId)
  {
    var id = commitId.ToString(CultureInfo.InvariantCulture);

    return JobRequest.Create(
      JobTypes.NotifyCommit,
      JsonSerializer.Serialize(new { CommitId = commitId, Force = true }),
      $"commit:{id}",
      $"notify:{id}:forced",
      coalesce: true
    );
  }

  #endregion
}