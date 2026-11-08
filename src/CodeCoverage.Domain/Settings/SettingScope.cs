// ------------------------------------------------------------------------------------------------
// <copyright file="SettingScope.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Settings;

/// <summary>The level at which the value of a setting applies.</summary>
public enum SettingScope
{
  /// <summary>
  ///   One value for the whole instance, edited by an administrator of the instance and read through
  ///   <c>IOptionsMonitor&lt;T&gt;</c>.
  /// </summary>
  Instance = 0,

  /// <summary>One value per organization, which the settings of the instance neither list nor change.</summary>
  Organization = 1,
}
