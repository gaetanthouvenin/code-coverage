// ------------------------------------------------------------------------------------------------
// <copyright file="CheckConnectionsCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Connections.Commands.CheckConnections;

/// <summary>
///   Coordinates the check of the forge connections from the lane of the instance: the work of the daily
///   <c>CheckConnections</c> job, which queues one check per connection that is not suspended and reads no table of an
///   organization.
/// </summary>
/// <remarks>GitHub has no forge connection (ADR-017): its installations are not checked here.</remarks>
public sealed record CheckConnectionsCommand : ICommand, ISystemOnly;
