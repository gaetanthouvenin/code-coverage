// ------------------------------------------------------------------------------------------------
// <copyright file="NotificationDecision.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Notifications;

/// <summary>What the gate decides for a commit.</summary>
public enum NotificationDecision
{
  /// <summary>The commit is notified.</summary>
  Notify,

  /// <summary>
  ///   The commit is neither complete nor in error, or uploads were processed since its last finalization: it has
  ///   no result to notify yet, or the one it has is the report of a part of its uploads.
  /// </summary>
  NotFinalized,

  /// <summary>An upload of the commit is still received or being processed.</summary>
  UploadsInProgress,

  /// <summary>The configuration waits for a manual trigger (<c>codecov.notify.manual_trigger</c>).</summary>
  ManualTriggerExpected,

  /// <summary>The commit has fewer sessions than <c>codecov.notify.after_n_builds</c>.</summary>
  BuildsExpected,
}