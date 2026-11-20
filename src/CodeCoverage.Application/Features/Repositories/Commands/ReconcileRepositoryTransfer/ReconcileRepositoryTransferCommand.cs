// ------------------------------------------------------------------------------------------------
// <copyright file="ReconcileRepositoryTransferCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Repositories.Commands.ReconcileRepositoryTransfer;

/// <summary>
///   Closes, in the organization of the scope, the row of a repository transferred to another account: the
///   work of the <c>ReconcileRepositoryTransfer</c> job.
/// </summary>
/// <param name="Forge">The forge of the repository, which names its identifier.</param>
/// <param name="RepositoryExternalId">The identifier of the repository on its forge, in text.</param>
public sealed record ReconcileRepositoryTransferCommand(Forge Forge, string RepositoryExternalId) : ICommand, ISystemOnly;
