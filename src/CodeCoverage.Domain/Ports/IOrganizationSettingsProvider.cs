// ------------------------------------------------------------------------------------------------
// <copyright file="IOrganizationSettingsProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Domain.Ports;

/// <summary>The settings of the organization the current DI scope has entered.</summary>
/// <remarks>
///   These settings do not go through <c>IOptionsMonitor&lt;T&gt;</c>, which holds one value per process. The values
///   are kept for the DI scope only, so a change applies from the next scope, without a restart.
/// </remarks>
public interface IOrganizationSettingsProvider
{
  #region Methods

  /// <summary>Reads the settings of the current organization.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>For each setting, the value saved by the organization, otherwise the default of the catalog.</returns>
  /// <exception cref="InvalidOperationException">The scope has not entered an organization.</exception>
  Task<OrganizationSettingsSnapshot> GetAsync(CancellationToken cancellationToken);

  #endregion
}
