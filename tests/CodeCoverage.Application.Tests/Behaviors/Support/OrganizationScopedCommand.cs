// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationScopedCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// A command on one organization, addressed by its forge and its login.
internal sealed record OrganizationScopedCommand(Forge Forge, string Login) : ICommand<string>, IOrganizationScoped
{
  #region Properties

  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion
}
