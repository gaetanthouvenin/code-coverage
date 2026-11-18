// ------------------------------------------------------------------------------------------------
// <copyright file="SyncConnectionsCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Connections.Commands.SyncConnections;

/// <summary>
///   Coordinates the reconciliation of the forge connections from the lane of the instance: the work of the daily
///   <c>SyncConnections</c> job, which queues one job per active connection and reads no table of an organization.
/// </summary>
/// <remarks>GitHub keeps its own coordinator, <c>SyncInstallations</c>: it has no forge connection (ADR-017).</remarks>
public sealed record SyncConnectionsCommand : ICommand, ISystemOnly;
