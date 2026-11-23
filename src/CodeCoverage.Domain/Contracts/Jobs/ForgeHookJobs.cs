// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>The keys, the payloads and the jobs of the management of the hooks of the forge connections (ADR-017).</summary>
public static class ForgeHookJobs
{
  #region Fields

  /// <summary>
  ///   The key that serializes and merges the coordinators of the hooks, on the lane of the instance. The queue prefixes it
  ///   with the lane.
  /// </summary>
  public const string Key = "forge-hooks";

  #endregion

  #region Methods

  /// <summary>Builds the payload of the ensuring of the hooks of a forge connection.</summary>
  /// <param name="connectionId">The identifier of the connection.</param>
  /// <returns>The payload.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connectionId" /> is not well formed.</exception>
  public static string EnsurePayload(string connectionId)
  {
    ThrowIfInvalidConnection(connectionId);

    return JsonSerializer.Serialize(new { ConnectionId = connectionId });
  }

  /// <summary>Builds the key that serializes and merges the ensuring of the hooks of a forge connection.</summary>
  /// <param name="connectionId">The identifier of the connection.</param>
  /// <returns>The key, apart from the lock of the deliveries, of the reconciliations and of the checks of the connection.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connectionId" /> is not well formed.</exception>
  public static string EnsureKey(string connectionId)
  {
    ThrowIfInvalidConnection(connectionId);

    return $"hooks:connection:{connectionId}";
  }

  /// <summary>Builds the ensuring of the hooks of a forge connection, in the organization of the scope.</summary>
  /// <param name="connectionId">The identifier of the connection.</param>
  /// <returns>The job, merged with the one already waiting for the same connection.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connectionId" /> is not well formed.</exception>
  public static JobRequest Ensure(string connectionId)
  {
    var key = EnsureKey(connectionId);

    return JobRequest.Create(JobTypes.EnsureForgeHooks, EnsurePayload(connectionId), key, key, coalesce: true);
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
