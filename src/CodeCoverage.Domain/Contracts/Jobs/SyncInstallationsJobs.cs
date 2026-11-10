// ------------------------------------------------------------------------------------------------
// <copyright file="SyncInstallationsJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the reconciliation of the installations, which runs on the lane of the instance.</summary>
public static class SyncInstallationsJobs
{
  #region Fields

  /// <summary>The key that serializes and merges the reconciliations.</summary>
  public const string Key = "installations";

  #endregion

  #region Methods

  /// <summary>Builds the reconciliation of every installation that an event asks for, to run at once.</summary>
  /// <returns>The job, merged with the one an earlier event left waiting.</returns>
  /// <remarks>
  ///   Its deduplication key is not the one of the daily reconciliation: merged with it, the request of
  ///   an event would wait for the next day.
  /// </remarks>
  public static JobRequest Create() =>
    JobRequest.ForInstance(JobTypes.SyncInstallations, "{}", Key, $"{Key}:event", coalesce: true);

  /// <summary>Builds the reconciliation that follows the transfer of a repository to another account.</summary>
  /// <param name="repositoryExternalId">The identifier of the repository on its forge.</param>
  /// <param name="previousOwnerAccountId">The identifier, on the forge, of the account the repository left.</param>
  /// <returns>The job, merged with the one already waiting for the same repository and the same account.</returns>
  /// <remarks>
  ///   Its deduplication key holds the account as well as the repository: a merged job keeps its own
  ///   payload, so a second transfer of the repository, from another account, must not be merged with
  ///   the first one, or the row of that account would never be closed.
  /// </remarks>
  /// <exception cref="ArgumentException">An argument is empty.</exception>
  public static JobRequest CreateForTransfer(string repositoryExternalId, string previousOwnerAccountId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(repositoryExternalId);
    ArgumentException.ThrowIfNullOrWhiteSpace(previousOwnerAccountId);

    var payload = JsonSerializer.Serialize(
      new { TransferredRepositoryId = repositoryExternalId, PreviousOwnerAccountId = previousOwnerAccountId }
    );

    return JobRequest.ForInstance(JobTypes.SyncInstallations, payload, Key, $"{Key}:transfer:{repositoryExternalId}:{previousOwnerAccountId}", coalesce: true);
  }

  #endregion
}
