// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateOrganizationSettingCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations.Commands.UpdateOrganizationSetting;

/// <summary>Changes a setting of an organization; it applies from the next scope, without a restart.</summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <param name="Key">The key of the setting, of the scope of an organization.</param>
/// <param name="Value">The new value.</param>
/// <remarks>A setting of the instance is refused as unknown: it is changed by <c>UpdateSetting</c>, by an administrator of the instance.</remarks>
public sealed record UpdateOrganizationSettingCommand(Forge Forge, string Login, string Key, string Value) : ICommand, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion
}
