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

  /// <summary>Parses an upload that was stored at its reception.</summary>
  public const string ProcessUpload = "ProcessUpload";

  #endregion
}
