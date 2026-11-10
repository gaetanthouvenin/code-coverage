// ------------------------------------------------------------------------------------------------
// <copyright file="SyncInstallationsCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.SyncInstallations;

/// <summary>
///   Coordinates the reconciliation with GitHub from the lane of the instance: the work of the
///   <c>SyncInstallations</c> job, which queues one job per organization and reads none.
/// </summary>
/// <param name="TransferredRepositoryId">
///   The identifier, on GitHub, of a repository that was transferred to another account, or
///   <see langword="null" /> to reconcile every installation.
/// </param>
/// <param name="PreviousOwnerAccountId">The identifier, on GitHub, of the account the repository left.</param>
public sealed record SyncInstallationsCommand(string? TransferredRepositoryId = null, string? PreviousOwnerAccountId = null)
  : ICommand, ISystemOnly;