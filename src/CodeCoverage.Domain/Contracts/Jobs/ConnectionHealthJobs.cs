// ------------------------------------------------------------------------------------------------
// <copyright file="ConnectionHealthJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>The keys and the payloads of the daily check of the forge connections (ADR-017).</summary>
public static class ConnectionHealthJobs
{
  #region Fields

  /// <summary>
  ///   The key that serializes and merges the coordinators of the checks, on the lane of the instance. The queue prefixes
  ///   it with the lane.
  /// </summary>
  public const string Key = "connection-checks";

  #endregion

  #region Methods

  /// <summary>Builds the payload of the check of a forge connection.</summary>
  /// <param name="connectionId">The identifier of the connection.</param>
  /// <returns>The payload.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connectionId" /> is not well formed.</exception>
  public static string CheckPayload(string connectionId)
  {
    ThrowIfInvalidConnection(connectionId);

    return JsonSerializer.Serialize(new { ConnectionId = connectionId });
  }

  /// <summary>Builds the key that serializes and merges the checks of a forge connection.</summary>
  /// <param name="connectionId">The identifier of the connection.</param>
  /// <returns>The key, apart from the lock of the deliveries and of the reconciliations of the connection.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connectionId" /> is not well formed.</exception>
  public static string CheckKey(string connectionId)
  {
    ThrowIfInvalidConnection(connectionId);

    return $"check:connection:{connectionId}";
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
