// ------------------------------------------------------------------------------------------------
// <copyright file="SettingOrigin.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Settings;

/// <summary>Where the effective value of a setting comes from.</summary>
public enum SettingOrigin
{
  /// <summary>Saved from the console, in the database.</summary>
  Database,

  /// <summary>Given by the environment: read-only on screen.</summary>
  Environment,

  /// <summary>The default of the catalog.</summary>
  Default,

  /// <summary>The value stored for the organization of the page.</summary>
  Organization,
}
