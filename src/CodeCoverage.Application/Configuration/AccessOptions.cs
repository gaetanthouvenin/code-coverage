// ------------------------------------------------------------------------------------------------
// <copyright file="AccessOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Configuration;

/// <summary>The <c>Access</c> settings group: the access rules.</summary>
/// <remarks>Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart.</remarks>
public sealed class AccessOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.AccessGroup;

  #endregion

  #region Properties

  /// <summary>
  ///   Gets or sets the numeric GitHub user identifiers of the instance administrators, separated by
  ///   commas.
  /// </summary>
  /// <value><see langword="null" /> by default.</value>
  public string? AdminUserIds { get; set; }

  /// <summary>Gets or sets the time the GitHub permissions of a user are cached, in minutes.</summary>
  /// <value>10 by default.</value>
  public int CacheMinutes { get; set; } = 10;

  /// <summary>
  ///   Gets or sets the age, in minutes, beyond which a permission or a membership read from GitHub
  ///   grants nothing, even when it could not be refreshed.
  /// </summary>
  /// <value>60 by default.</value>
  public int HardExpiryMinutes { get; set; } = 60;

  #endregion
}
