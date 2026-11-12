// ------------------------------------------------------------------------------------------------
// <copyright file="JobTypes.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>The types of the background jobs, as stored in <see cref="JobRequest.Type" />.</summary>
/// <remarks>
///   <para>
///     Each feature that runs work in the background adds its constant here (for example
///     <c>ProcessUpload</c> or <c>FinalizeCommit</c>), together with its case in the job dispatcher of
///     the Infrastructure, which turns the JSON payload into the command to send.
///   </para>
///   <para>
///     A type is stored as text in the <c>Jobs</c> table: a constant is never renamed once released,
///     or the jobs already queued under the old name could no longer be dispatched. It is at most
///     <see cref="JobRequest.TypeMaxLength" /> characters long.
///   </para>
/// </remarks>
public static class JobTypes
{
  #region Fields

  /// <summary>
  ///   A diagnostic job that does nothing but run through the whole pipeline of the worker (claim, system
  ///   principal, dispatch, completion): checks that the worker of a host runs the jobs.
  /// </summary>
  public const string Ping = "Ping";

  /// <summary>Handles a webhook delivery of GitHub that was stored at its reception.</summary>
  public const string HandleWebhook = "HandleWebhook";

  /// <summary>Lists the installations of the App and queues the reconciliation of each one, in its organization.</summary>
  public const string SyncInstallations = "SyncInstallations";

  /// <summary>Reconciles the repositories of an organization with those its installation serves.</summary>
  public const string SyncInstallationRepositories = "SyncInstallationRepositories";

  /// <summary>Closes, in the organization it left, the row of a repository transferred to another account.</summary>
  public const string ReconcileRepositoryTransfer = "ReconcileRepositoryTransfer";

  /// <summary>Parses an upload that was stored at its reception.</summary>
  public const string ProcessUpload = "ProcessUpload";

  /// <summary>Rebuilds the report of a commit from its uploads.</summary>
  public const string FinalizeCommit = "FinalizeCommit";

  /// <summary>Publishes the statuses of a commit, then its comment.</summary>
  public const string NotifyCommit = "NotifyCommit";

  /// <summary>Puts the ended uploads of a commit back in the queue, so that they are parsed again from their raw files.</summary>
  public const string ReprocessCommit = "ReprocessCommit";

  /// <summary>
  ///   Rewrites the owner of the repositories of an organization, a copy for display, after its account was
  ///   renamed.
  /// </summary>
  public const string RewriteRepositoryOwner = "RewriteRepositoryOwner";

  /// <summary>Reads a pull request again from its forge and records it.</summary>
  public const string SyncPullRequest = "SyncPullRequest";

  /// <summary>
  ///   Notifies by force the commits that were never notified long after their last upload: a coordinator on the lane
  ///   of the instance, and one job of the same type per active organization.
  /// </summary>
  public const string SweepPendingCommits = "SweepPendingCommits";

  /// <summary>Asks the forge again which organizations a user is linked to, and with which role.</summary>
  public const string RefreshUserMemberships = "RefreshUserMemberships";

  /// <summary>Asks the forge again which repositories of the organization of the job a user may read, and how.</summary>
  public const string RefreshUserAccess = "RefreshUserAccess";

  /// <summary>
  ///   Deletes what the retention no longer keeps: a coordinator on the lane of the instance, which purges the operational
  ///   tables, and one job of the same type per active organization.
  /// </summary>
  public const string PurgeExpiredData = "PurgeExpiredData";

  #endregion
}
