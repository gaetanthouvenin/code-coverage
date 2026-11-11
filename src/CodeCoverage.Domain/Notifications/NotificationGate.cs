// ------------------------------------------------------------------------------------------------
// <copyright file="NotificationGate.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Notifications;

/// <summary>Decides when a commit is notified (ARCHITECTURE §7.6). Pure.</summary>
public static class NotificationGate
{
  #region Methods

  /// <summary>Decides whether a commit is notified now.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="uploadsInProgress">How many uploads of the commit are received or being processed.</param>
  /// <param name="uploadsProcessed">How many uploads of the commit were processed.</param>
  /// <param name="rule">The notification rule of the configuration.</param>
  /// <param name="countCarriedForward">Whether the carried-forward sessions count towards <c>after_n_builds</c>.</param>
  /// <param name="forced">Whether the notification was asked for by hand (<c>uploads-complete</c>).</param>
  /// <returns><see cref="NotificationDecision.Notify" />, or the reason to wait.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="commit" /> or <paramref name="rule" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="uploadsInProgress" /> or <paramref name="uploadsProcessed" /> is negative.</exception>
  /// <remarks>
  ///   <para>
  ///     A forced notification ignores <c>manual_trigger</c> and <c>after_n_builds</c>; it still waits for
  ///     the uploads in progress, so that it never notifies a part of what was sent.
  ///   </para>
  ///   <para>
  ///     A commit that is complete stays so when another upload arrives. Once that upload is processed, and
  ///     until the finalization it queued has run, the report of the commit does not hold it:
  ///     <paramref name="uploadsProcessed" /> then differs from <see cref="Commit.UploadedSessionCount" />, what
  ///     the last finalization merged, and nothing is notified from that report, forced or not.
  ///   </para>
  ///   <para>
  ///     With <paramref name="countCarriedForward" />, every session counts, as <c>len(report.sessions)</c>
  ///     of Codecov: a pull request that only touches one part of a monorepo reaches the count through
  ///     what it carries forward.
  ///   </para>
  /// </remarks>
  public static NotificationDecision Decide(
    Commit commit,
    int uploadsInProgress,
    int uploadsProcessed,
    NotifyRule rule,
    bool countCarriedForward,
    bool forced)
  {
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(rule);
    ArgumentOutOfRangeException.ThrowIfNegative(uploadsInProgress);
    ArgumentOutOfRangeException.ThrowIfNegative(uploadsProcessed);

    if (commit.State is not (CommitState.Complete or CommitState.Error))
    {
      return NotificationDecision.NotFinalized;
    }

    if (uploadsInProgress > 0)
    {
      return NotificationDecision.UploadsInProgress;
    }

    if (uploadsProcessed != commit.UploadedSessionCount)
    {
      return NotificationDecision.NotFinalized;
    }

    if (forced)
    {
      return NotificationDecision.Notify;
    }

    if (rule.ManualTrigger)
    {
      return NotificationDecision.ManualTriggerExpected;
    }

    var sessions = countCarriedForward ? commit.SessionCount : commit.UploadedSessionCount;

    return sessions >= rule.AfterNBuilds ? NotificationDecision.Notify : NotificationDecision.BuildsExpected;
  }

  /// <summary>Gives what the statuses of a commit notified by force say when it did not receive every upload it expected.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="rule">The <c>codecov.notify</c> rule.</param>
  /// <param name="countCarriedForward">Whether the sessions carried forward count for <c>after_n_builds</c>.</param>
  /// <returns>
  ///   The suffix <c> (partial: n/m uploads)</c>, with the sessions counted and <c>after_n_builds</c>, or
  ///   <see langword="null" /> when <c>after_n_builds</c> is reached.
  /// </returns>
  /// <remarks>The sessions are counted as <see cref="Decide" /> counts them.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="commit" /> or <paramref name="rule" /> is <see langword="null" />.</exception>
  public static string? PartialSuffix(Commit commit, NotifyRule rule, bool countCarriedForward)
  {
    ArgumentNullException.ThrowIfNull(commit);
    ArgumentNullException.ThrowIfNull(rule);

    var sessions = countCarriedForward ? commit.SessionCount : commit.UploadedSessionCount;

    return sessions >= rule.AfterNBuilds
             ? null
             : string.Create(System.Globalization.CultureInfo.InvariantCulture, $" (partial: {sessions}/{rule.AfterNBuilds} uploads)");
  }

  #endregion
}