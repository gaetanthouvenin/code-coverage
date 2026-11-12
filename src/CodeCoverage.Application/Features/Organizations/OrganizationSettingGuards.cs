// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettingGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Organizations;

/// <summary>
///   The refusals of the settings of an organization, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the message of each.
/// </summary>
internal static class OrganizationSettingGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>Throws when a key is not a setting of the scope of an organization: unknown, or a setting of the instance.</summary>
    /// <param name="definition">The definition of the key, or <see langword="null" /> when the catalog has none.</param>
    /// <param name="key">The key asked for.</param>
    /// <exception cref="NotFoundException">The key names no setting of an organization (<see cref="SettingErrors.Unknown" />).</exception>
    public static void ThrowIfNotOrganizationSetting(SettingDefinition? definition, string key)
    {
      if (definition is not { Scope: SettingScope.Organization })
      {
        throw new NotFoundException(SettingErrors.Unknown, $"The setting '{key}' does not exist for an organization.");
      }
    }
  }

  #endregion
}
