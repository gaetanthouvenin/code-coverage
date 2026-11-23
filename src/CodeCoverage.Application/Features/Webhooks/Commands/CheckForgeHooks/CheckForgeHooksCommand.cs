// ------------------------------------------------------------------------------------------------
// <copyright file="CheckForgeHooksCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Webhooks.Commands.CheckForgeHooks;

/// <summary>
///   Coordinates the hooks of the forge connections from the lane of the instance: the work of the daily
///   <c>CheckForgeHooks</c> job, which queues one <c>EnsureForgeHooks</c> per connection that is not suspended and reads no
///   table of an organization.
/// </summary>
/// <remarks>
///   The daily pass also brings to the forge the webhook secret of a connection after its rotation (M7.13). GitHub has no
///   forge connection (ADR-017): the App delivers the events of its installations.
/// </remarks>
public sealed record CheckForgeHooksCommand : ICommand, ISystemOnly;
