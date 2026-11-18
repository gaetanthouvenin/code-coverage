// ------------------------------------------------------------------------------------------------
// <copyright file="SyncConnectionsJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>The keys of the daily reconciliation of the forge connections, which runs on the lane of the instance (ADR-017).</summary>
public static class SyncConnectionsJobs
{
  #region Fields

  /// <summary>The key that serializes and merges the reconciliations of the forge connections.</summary>
  public const string Key = "connections";

  #endregion
}
