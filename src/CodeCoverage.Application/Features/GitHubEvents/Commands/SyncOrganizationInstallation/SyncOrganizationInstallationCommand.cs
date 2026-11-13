// ------------------------------------------------------------------------------------------------
// <copyright file="SyncOrganizationInstallationCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.SyncOrganizationInstallation;

/// <summary>Asks for the repositories of the installations of an organization to be read again from the forge.</summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <remarks>
///   It only queues the job the webhooks already use, one per installation of the organization, with the key that
///   merges it with the one already waiting: no forge is called in the request (critical rule 3). The global
///   <c>SyncInstallations</c> coordinator stays, for the webhooks and the periodic run.
/// </remarks>
public sealed record SyncOrganizationInstallationCommand(Forge Forge, string Login) : ICommand, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion
}
