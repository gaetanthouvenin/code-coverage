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

  /// <summary>Gets or sets the GitHub logins of the administrators, separated by commas.</summary>
  /// <value><see langword="null" /> by default.</value>
  public string? AdminLogins { get; set; }

  /// <summary>Gets or sets the time the GitHub permissions of a user are cached, in minutes.</summary>
  /// <value>10 by default.</value>
  public int CacheMinutes { get; set; } = 10;

  #endregion
}
