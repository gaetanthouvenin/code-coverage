// ------------------------------------------------------------------------------------------------
// <copyright file="SyncPullRequestJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the job that reads a pull request again from its forge.</summary>
public static class SyncPullRequestJobs
{
  #region Methods

  /// <summary>Builds the synchronization of one pull request, in the organization of the scope.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <returns>The job, merged with the one already waiting for the same pull request.</returns>
  /// <remarks>
  ///   Merging loses nothing: the job reads the forge, not what the event that asked for it said. Its lock
  ///   serializes the synchronizations of one pull request.
  /// </remarks>
  /// <exception cref="ArgumentOutOfRangeException">An argument is zero or negative.</exception>
  public static JobRequest Create(long repositoryId, int number)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repositoryId);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);

    var key = string.Create(CultureInfo.InvariantCulture, $"{repositoryId}:{number}");

    return JobRequest.Create(
      JobTypes.SyncPullRequest,
      JsonSerializer.Serialize(new { RepositoryId = repositoryId, Number = number }),
      $"pull:{key}",
      $"syncpr:{key}",
      coalesce: true
    );
  }

  #endregion
}
