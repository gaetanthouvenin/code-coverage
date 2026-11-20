// ------------------------------------------------------------------------------------------------
// <copyright file="CheckConnectionCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Connections.Commands.CheckConnection;

/// <summary>
///   Checks a forge connection of the organization of the scope against its forge and records its health: the work of
///   the <c>CheckConnection</c> job.
/// </summary>
/// <param name="ConnectionId">The identifier of the forge connection (ADR-017).</param>
public sealed record CheckConnectionCommand(string ConnectionId) : ICommand, ISystemOnly;
