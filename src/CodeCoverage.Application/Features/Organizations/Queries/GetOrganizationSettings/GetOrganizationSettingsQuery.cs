// ------------------------------------------------------------------------------------------------
// <copyright file="GetOrganizationSettingsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Organizations.Queries.GetOrganizationSettings;

/// <summary>Reads the settings of an organization: its stored value, or the default of the catalog.</summary>
/// <param name="Forge">The forge of the organization.</param>
/// <param name="Login">The login of the organization.</param>
/// <remarks>The four keys of the scope of an organization, by key; none is secret.</remarks>
public sealed record GetOrganizationSettingsQuery(Forge Forge, string Login) : IQuery<IReadOnlyList<SettingValue>>, IOrganizationScoped
{
  #region Properties

  /// <inheritdoc />
  public OrganizationRole RequiredRole => OrganizationRole.Admin;

  #endregion
}
