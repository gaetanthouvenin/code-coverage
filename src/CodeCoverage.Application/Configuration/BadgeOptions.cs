// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Configuration;

/// <summary>The <c>Badges</c> settings group: the badges.</summary>
/// <remarks>Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart.</remarks>
public sealed class BadgeOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.BadgesGroup;

  #endregion

  #region Properties

  /// <summary>Gets or sets the label shown on the left of the badges.</summary>
  /// <value><c>coverage</c> by default.</value>
  public string Label { get; set; } = "coverage";

  #endregion
}
