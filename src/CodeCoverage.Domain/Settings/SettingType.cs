// ------------------------------------------------------------------------------------------------
// <copyright file="SettingType.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Settings;

/// <summary>
///   The kind of value a setting holds, which decides its validation and its field on screen.
/// </summary>
/// <remarks>
///   Whatever its type, a value is stored and read as text, written with the invariant culture. A
///   secret is not a type of its own: it is a <see cref="Text" /> setting marked
///   <see cref="SettingDefinition.IsSecret" />.
/// </remarks>
public enum SettingType
{
  /// <summary>Free text.</summary>
  Text = 0,

  /// <summary>An absolute <c>http</c> or <c>https</c> URL.</summary>
  Url = 1,

  /// <summary>
  ///   An integer, optionally bounded by <see cref="SettingDefinition.Min" /> and
  ///   <see cref="SettingDefinition.Max" />.
  /// </summary>
  Number = 2,

  /// <summary><c>true</c> or <c>false</c>.</summary>
  Boolean = 3,

  /// <summary>A list of values separated by commas (for example GitHub logins).</summary>
  Csv = 4,

  /// <summary>A YAML document, edited as multi-line text.</summary>
  Yaml = 5,
}
