// ------------------------------------------------------------------------------------------------
// <copyright file="NotifyCommitCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Notifications.Commands.NotifyCommit;

/// <summary>Notifies a commit on its forge, when the gate lets it through: the work of the <c>NotifyCommit</c> job.</summary>
/// <param name="CommitId">The identifier of the commit.</param>
/// <param name="Force">
///   Whether the notification was asked for by hand (<c>uploads-complete</c>): it ignores
///   <c>after_n_builds</c> and <c>manual_trigger</c>, and waits for the uploads in progress.
/// </param>
/// <param name="Partial">
///   Whether the notification comes from the sweep of the stuck commits: its statuses say how many uploads were
///   received when <c>after_n_builds</c> is not reached.
/// </param>
/// <remarks>Sent by the job only, which holds the lock <c>commit:{id}</c>: the report is never rebuilt meanwhile.</remarks>
public sealed record NotifyCommitCommand(long CommitId, bool Force = false, bool Partial = false) : ICommand, ISystemOnly;