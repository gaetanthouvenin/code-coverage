// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Configuration;

/// <summary>The <c>Coverage</c> settings group: the coverage rules.</summary>
/// <remarks>Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart.</remarks>
public sealed class CoverageOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.CoverageGroup;

  #endregion

  #region Properties

  /// <summary>Gets or sets the YAML configuration shared by every repository of the organization.</summary>
  /// <value><see langword="null" /> by default.</value>
  public string? OrganizationYaml { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether a report path may match a repository file by its name alone.
  /// </summary>
  /// <value><see langword="false" /> by default.</value>
  public bool AllowFilenameOnlyMatch { get; set; }

  #endregion
}
