// ------------------------------------------------------------------------------------------------
// <copyright file="SweepPendingCommitsJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Names the keys of the sweep of the commits that were never notified.</summary>
public static class SweepPendingCommitsJobs
{
  #region Fields

  /// <summary>
  ///   The key that serializes and merges the sweeps. The queue prefixes it with the lane: the coordinator and the
  ///   sweep of each organization never merge nor wait for one another.
  /// </summary>
  public const string Key = "sweep";

  #endregion
}
