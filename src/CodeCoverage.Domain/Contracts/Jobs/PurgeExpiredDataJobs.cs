// ------------------------------------------------------------------------------------------------
// <copyright file="PurgeExpiredDataJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>The keys of the daily purge (ARCHITECTURE §4.4).</summary>
public static class PurgeExpiredDataJobs
{
  #region Fields

  /// <summary>
  ///   The key that serializes and merges the purges. The queue prefixes it with the lane: the coordinator and the purge
  ///   of each organization never merge nor wait for one another.
  /// </summary>
  public const string Key = "purge";

  /// <summary>How many rows a purge deletes at most in one statement.</summary>
  public const int BatchSize = 5000;

  #endregion
}
