// ------------------------------------------------------------------------------------------------
// <copyright file="ResetOrganizationSettingCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations.Commands.ResetOrganizationSetting;

/// <summary>Puts a setting of an organization back to the default of the catalog.</summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <param name="Key">The key of the setting, of the scope of an organization.</param>
public sealed record ResetOrganizationSettingCommand(Forge Forge, string Login, string Key) : ICommand, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion
}
