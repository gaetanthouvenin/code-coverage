// ------------------------------------------------------------------------------------------------
// <copyright file="FinalizationWakeUps.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Domain.Contracts.Commits;

/// <summary>The jobs a finalization queues in its own transaction (ARCHITECTURE §6.5, step 4.3, and §7.6).</summary>
/// <param name="Completed">
///   <see langword="true" /> when the commit becomes complete and was not before: it wakes the commits that passed it
///   while it was pending and still wait for it (#165), its real descendants only.
/// </param>
/// <param name="PendingAncestorIds">
///   The pending ancestors the commit passed after the wait, the nearest first, in the order of the chain: it locks them
///   in that order, and queues its own finalization again when one of them has ended since it read it.
/// </param>
/// <param name="Notification">
///   The notification of the commit, queued in the same transaction, or <see langword="null" /> when the commit is not
///   notified yet.
/// </param>
public sealed record FinalizationWakeUps(bool Completed, IReadOnlyList<long> PendingAncestorIds, JobRequest? Notification = null)
{
  #region Properties

  /// <summary>
  ///   Gets the notifications of the heads of the pull requests that forked from the commit and were compared to
  ///   something else: their comparison changes now that the commit has its report.
  /// </summary>
  public IReadOnlyList<JobRequest> DependentNotifications { get; init; } = [];

  /// <summary>Gets the finalization that queues nothing.</summary>
  public static FinalizationWakeUps None { get; } = new(false, []);

  #endregion
}
