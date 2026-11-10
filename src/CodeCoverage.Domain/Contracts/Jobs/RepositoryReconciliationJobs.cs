// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryReconciliationJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the jobs that reconcile the repositories of one organization with GitHub.</summary>
/// <remarks>
///   Each one runs inside its organization. The coordinator of the lane of the instance queues them by
///   naming the organization; a delivery handled inside an organization queues them for its own scope.
/// </remarks>
public static class RepositoryReconciliationJobs
{
  #region Methods

  /// <summary>Builds the payload of the reconciliation of the repositories of an installation.</summary>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <returns>The payload.</returns>
  public static string InstallationPayload(long installationId) =>
    JsonSerializer.Serialize(new { InstallationId = installationId });

  /// <summary>Builds the key that serializes and merges the reconciliations of an installation.</summary>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <returns>The key, which is also the lock of the deliveries of the installation.</returns>
  public static string InstallationKey(long installationId) =>
    string.Create(CultureInfo.InvariantCulture, $"installation:{installationId}");

  /// <summary>Builds the reconciliation of the repositories of an installation, for the organization of the scope.</summary>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <returns>The job, merged with the one already waiting.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="installationId" /> is not positive.</exception>
  public static JobRequest ForInstallation(long installationId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(installationId);

    var key = InstallationKey(installationId);

    return JobRequest.Create(JobTypes.SyncInstallationRepositories, InstallationPayload(installationId), key, $"sync:{key}", coalesce: true);
  }

  /// <summary>Builds the payload of the closing of a repository transferred to another account.</summary>
  /// <param name="repositoryExternalId">The identifier of the repository on its forge.</param>
  /// <returns>The payload.</returns>
  /// <exception cref="ArgumentException"><paramref name="repositoryExternalId" /> is empty.</exception>
  public static string TransferPayload(string repositoryExternalId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(repositoryExternalId);

    return JsonSerializer.Serialize(new { RepositoryExternalId = repositoryExternalId });
  }

  /// <summary>Builds the key that serializes the work on a repository, as the deliveries of that repository do.</summary>
  /// <param name="repositoryExternalId">The identifier of the repository on its forge.</param>
  /// <returns>The key.</returns>
  /// <exception cref="ArgumentException"><paramref name="repositoryExternalId" /> is empty.</exception>
  public static string RepositoryKey(string repositoryExternalId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(repositoryExternalId);

    return $"repo:{repositoryExternalId}";
  }

  #endregion
}
