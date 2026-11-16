// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Connections;

/// <summary>The health of a forge connection, stored as text.</summary>
public enum ForgeConnectionStatus
{
  /// <summary>The last check succeeded and the credential does not expire soon.</summary>
  Active,

  /// <summary>The last check failed: the forge refused the credential or could not be reached.</summary>
  Failing,

  /// <summary>The last check succeeded, but the credential expires soon.</summary>
  Expiring,

  /// <summary>The connection was suspended: nothing reaches the forge through it.</summary>
  Suspended,
}
