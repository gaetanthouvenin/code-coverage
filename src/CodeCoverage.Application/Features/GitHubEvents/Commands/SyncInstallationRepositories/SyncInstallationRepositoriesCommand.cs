// ------------------------------------------------------------------------------------------------
// <copyright file="SyncInstallationRepositoriesCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.SyncInstallationRepositories;

/// <summary>
///   Reconciles the tracked repositories of the organization of the scope with those its installation
///   serves: the work of the <c>SyncInstallationRepositories</c> job.
/// </summary>
/// <param name="InstallationId">The identifier of the installation.</param>
public sealed record SyncInstallationRepositoriesCommand(long InstallationId) : ICommand, ISystemOnly;