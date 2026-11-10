// ------------------------------------------------------------------------------------------------
// <copyright file="FinalizationWakeUps.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Commits;

/// <summary>The finalizations a finalization queues in its own transaction (ARCHITECTURE §6.5, step 4.3).</summary>
/// <param name="Completed">
///   <see langword="true" /> when the commit becomes complete and was not before: it wakes the commits of its repository
///   that used a farther ancestor (10 at most).
/// </param>
/// <param name="PendingAncestorIds">
///   The pending ancestors the commit passed after the wait, the nearest first, in the order of the chain: it locks them
///   in that order, and queues its own finalization again when one of them has ended since it read it.
/// </param>
public sealed record FinalizationWakeUps(bool Completed, IReadOnlyList<long> PendingAncestorIds)
{
  #region Properties

  /// <summary>Gets the finalization that queues nothing.</summary>
  public static FinalizationWakeUps None { get; } = new(false, []);

  #endregion
}
