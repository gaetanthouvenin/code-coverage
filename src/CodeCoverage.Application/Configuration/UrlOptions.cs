// ------------------------------------------------------------------------------------------------
// <copyright file="UrlOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Configuration;

/// <summary>The <c>Urls</c> settings group: the public addresses of the hosts.</summary>
/// <remarks>Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart.</remarks>
public sealed class UrlOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.UrlsGroup;

  #endregion

  #region Properties

  /// <summary>Gets or sets the public address of the Api, or <see langword="null" /> before it is set.</summary>
  public string? ApiPublicUrl { get; set; }

  /// <summary>Gets or sets the public address of the Web console, or <see langword="null" /> before it is set.</summary>
  public string? WebPublicUrl { get; set; }

  #endregion
}
