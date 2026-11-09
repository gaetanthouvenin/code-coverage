// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Configuration;

/// <summary>The instance-level settings of the <c>Coverage</c> group: the coverage rules.</summary>
/// <remarks>
///   Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart. The
///   settings of the group set per organization are read through <c>IOrganizationSettingsProvider</c>.
/// </remarks>
public sealed class CoverageOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.CoverageGroup;

  #endregion

  #region Properties

  /// <summary>
  ///   Gets or sets a value indicating whether a report path may match a repository file by its name alone.
  /// </summary>
  /// <value><see langword="false" /> by default.</value>
  public bool AllowFilenameOnlyMatch { get; set; }

  #endregion
}
