// ------------------------------------------------------------------------------------------------
// <copyright file="ReconcileRepositoryTransferCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.ReconcileRepositoryTransfer;

/// <summary>
///   Closes, in the organization of the scope, the row of a repository transferred to another account: the
///   work of the <c>ReconcileRepositoryTransfer</c> job.
/// </summary>
/// <param name="RepositoryExternalId">The identifier of the repository on GitHub.</param>
public sealed record ReconcileRepositoryTransferCommand(string RepositoryExternalId) : ICommand, ISystemOnly;