// ------------------------------------------------------------------------------------------------
// <copyright file="SyncOrganizationConnectionCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Connections.Commands.SyncOrganizationConnection;

/// <summary>Asks for the repositories of an organization to be read again from its forge, whatever the forge.</summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <remarks>
///   It only queues the job the webhooks and the periodic run already use, one per installation of the GitHub App
///   on GitHub and one per connection that is not suspended on the other forges (ADR-017), with the key that merges
///   it with the one already waiting: no forge is called in the request (critical rule 3). The global coordinators
///   <c>SyncInstallations</c> and <c>SyncConnections</c> stay, for the webhooks and the periodic run.
/// </remarks>
public sealed record SyncOrganizationConnectionCommand(Forge Forge, string Login) : ICommand, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion
}
