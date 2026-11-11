// ------------------------------------------------------------------------------------------------
// <copyright file="SweepPendingCommitsCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Notifications.Commands.SweepPendingCommits;

/// <summary>
///   Notifies by force the commits that were never notified long after their last upload: the work of the
///   <c>SweepPendingCommits</c> job.
/// </summary>
/// <remarks>
///   The same job runs on two lanes (ADR-013). On the lane of the instance, every 5 minutes, it is a coordinator:
///   it queues one job per active organization. Inside an organization, it sweeps the commits of that organization.
/// </remarks>
public sealed record SweepPendingCommitsCommand : ICommand, ISystemOnly;
