// ------------------------------------------------------------------------------------------------
// <copyright file="SyncConnectionRepositoriesCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Connections.Commands.SyncConnectionRepositories;

/// <summary>
///   Reconciles the tracked repositories of the organization of the scope with those its connection serves, whatever
///   its forge: the work of the <c>SyncConnectionRepositories</c> job.
/// </summary>
/// <param name="Forge">The forge of the connection, which tells how to read <paramref name="ConnectionId" />.</param>
/// <param name="ConnectionId">
///   The identifier of the connection: the installation identifier in text on GitHub, the identifier of the forge
///   connection on the other forges (ADR-017).
/// </param>
public sealed record SyncConnectionRepositoriesCommand(Forge Forge, string ConnectionId) : ICommand, ISystemOnly;
