// ------------------------------------------------------------------------------------------------
// <copyright file="SettingSource.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Settings;

/// <summary>Where the effective value of a setting comes from.</summary>
/// <remarks>
///   The priority is: command line, then environment variables, then <c>appsettings.json</c>, then
///   the settings store, then the default of the catalog. Only a value that comes from the store, or
///   no value at all, can be changed on screen.
/// </remarks>
public enum SettingSource
{
  /// <summary>The value was saved in the settings store, from the screen or the API.</summary>
  Store = 0,

  /// <summary>
  ///   A stronger source provides the value (command line, environment variable or
  ///   <c>appsettings.json</c>): it is read-only on screen.
  /// </summary>
  Environment = 1,

  /// <summary>No source provides a value: the default of the catalog applies, if any.</summary>
  Default = 2,
}
