// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryReconciliationJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the jobs that reconcile the repositories of one organization with its forge.</summary>
/// <remarks>
///   Each one runs inside its organization. The coordinator of the lane of the instance queues them by
///   naming the organization; a delivery handled inside an organization queues them for its own scope.
/// </remarks>
public static class RepositoryReconciliationJobs
{
  #region Methods

  /// <summary>Builds the payload of the reconciliation of the repositories of an installation.</summary>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <returns>The payload: the one of the GitHub connection of the installation.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="installationId" /> is not positive.</exception>
  /// <remarks>
  ///   The jobs of the former type <see cref="JobTypes.SyncInstallationRepositories" /> carried
  ///   <c>{"InstallationId":42}</c>; the job dispatcher still reads them for one version.
  /// </remarks>
  public static string InstallationPayload(long installationId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(installationId);

    return ConnectionPayload(Forge.GitHub, TrackedRepository.ConnectionIdOf(installationId));
  }

  /// <summary>Builds the key that serializes and merges the reconciliations of an installation.</summary>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <returns>The key, which is also the lock of the deliveries of the installation.</returns>
  public static string InstallationKey(long installationId) =>
    string.Create(CultureInfo.InvariantCulture, $"installation:{installationId}");

  /// <summary>Builds the reconciliation of the repositories of an installation, for the organization of the scope.</summary>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <returns>The job, merged with the one already waiting.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="installationId" /> is not positive.</exception>
  /// <remarks>
  ///   The GitHub form of <see cref="ForConnection" />: the same job type, but its key stays <c>installation:{id}</c>,
  ///   the lock that the deliveries of the installation take.
  /// </remarks>
  public static JobRequest ForInstallation(long installationId)
  {
    var payload = InstallationPayload(installationId);
    var key = InstallationKey(installationId);

    return JobRequest.Create(JobTypes.SyncConnectionRepositories, payload, key, $"sync:{key}", coalesce: true);
  }

  /// <summary>Builds the payload of the reconciliation of the repositories of a connection.</summary>
  /// <param name="forge">The forge of the connection, which tells how to read its identifier.</param>
  /// <param name="connectionId">The identifier of the connection: the installation identifier in text on GitHub.</param>
  /// <returns>The payload.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connectionId" /> is not well formed.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="forge" /> is not a known forge.</exception>
  public static string ConnectionPayload(Forge forge, string connectionId)
  {
    ArgumentOutOfRangeException.ThrowIfUndefined(forge);
    ThrowIfInvalidConnection(connectionId);

    return JsonSerializer.Serialize(new { ConnectionId = connectionId, Forge = forge.ToString() });
  }

  /// <summary>Builds the key that serializes and merges the reconciliations of a forge connection.</summary>
  /// <param name="connectionId">The identifier of the connection.</param>
  /// <returns>The key, which is also the lock of the deliveries of the connection.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connectionId" /> is not well formed.</exception>
  public static string ConnectionKey(string connectionId)
  {
    ThrowIfInvalidConnection(connectionId);

    return $"connection:{connectionId}";
  }

  /// <summary>Builds the reconciliation of the repositories of a forge connection, for the organization of the scope.</summary>
  /// <param name="forge">The forge of the connection, never GitHub.</param>
  /// <param name="connectionId">The identifier of the connection (ADR-017).</param>
  /// <returns>The job, merged with the one already waiting.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="connectionId" /> is not well formed, or <paramref name="forge" /> is GitHub.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="forge" /> is not a known forge.</exception>
  /// <remarks>A GitHub installation keeps <see cref="ForInstallation" /> and its own key.</remarks>
  public static JobRequest ForConnection(Forge forge, string connectionId)
  {
    ArgumentException.ThrowIf(forge is Forge.GitHub, "A GitHub installation is reconciled by its own job.", nameof(forge));

    var payload = ConnectionPayload(forge, connectionId);
    var key = ConnectionKey(connectionId);

    return JobRequest.Create(JobTypes.SyncConnectionRepositories, payload, key, $"sync:{key}", coalesce: true);
  }

  /// <summary>Builds the payload of the closing of a repository transferred to another account.</summary>
  /// <param name="forge">The forge of the repository, which names its identifier.</param>
  /// <param name="repositoryExternalId">The identifier of the repository on its forge.</param>
  /// <returns>The payload.</returns>
  /// <exception cref="ArgumentException"><paramref name="repositoryExternalId" /> is empty.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="forge" /> is not a known forge.</exception>
  /// <remarks>
  ///   The jobs queued before the forge was named carried <c>{"RepositoryExternalId":"7"}</c>; the job dispatcher reads
  ///   them as GitHub ones for one version.
  /// </remarks>
  public static string TransferPayload(Forge forge, string repositoryExternalId)
  {
    ArgumentOutOfRangeException.ThrowIfUndefined(forge);
    ArgumentException.ThrowIfNullOrWhiteSpace(repositoryExternalId);

    return JsonSerializer.Serialize(new { Forge = forge.ToString(), RepositoryExternalId = repositoryExternalId });
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

  /// <summary>Checks the identifier of a forge connection.</summary>
  /// <param name="connectionId">The identifier.</param>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connectionId" /> is not well formed.</exception>
  private static void ThrowIfInvalidConnection(string connectionId)
  {
    ArgumentNullException.ThrowIfNull(connectionId);
    ArgumentException.ThrowIf(
      !ForgeConnection.IsWellFormedId(connectionId),
      $"The connection identifier '{connectionId}' is not well formed.",
      nameof(connectionId));
  }

  #endregion
}
