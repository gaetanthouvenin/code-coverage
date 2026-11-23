// ------------------------------------------------------------------------------------------------
// <copyright file="EnsureForgeHooksCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Webhooks.Commands.EnsureForgeHooks;

/// <summary>
///   Creates, through the API of its forge, the hooks a forge connection of the organization of the scope misses, and
///   records the state of each in <c>ForgeHooks</c>: the work of the <c>EnsureForgeHooks</c> job.
/// </summary>
/// <param name="ConnectionId">The identifier of the forge connection (ADR-017).</param>
public sealed record EnsureForgeHooksCommand(string ConnectionId) : ICommand, ISystemOnly;
