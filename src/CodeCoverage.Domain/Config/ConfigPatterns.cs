// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigPatterns.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Config;

/// <summary>Reads the lists of patterns of a configuration.</summary>
internal static class ConfigPatterns
{
  #region Methods

  /// <summary>Reads a <c>paths</c> list.</summary>
  /// <param name="list">The list, if it is present.</param>
  /// <param name="key">Where the list is, for the warnings.</param>
  /// <param name="warnings">Receives what cannot be applied as written.</param>
  /// <returns>The patterns, those that cannot run included.</returns>
  internal static IReadOnlyList<PathPattern> ReadPaths(
    ConfigNode? list,
    string key,
    ICollection<ConfigWarning> warnings) =>
    Read(list, key, warnings, PathPattern.Parse);

  /// <summary>Reads an <c>ignore</c> list.</summary>
  /// <param name="list">The list, if it is present.</param>
  /// <param name="key">Where the list is, for the warnings.</param>
  /// <param name="warnings">Receives what cannot be applied as written.</param>
  /// <returns>The patterns inverted, ready to join a set.</returns>
  internal static IReadOnlyList<PathPattern> ReadIgnores(
    ConfigNode? list,
    string key,
    ICollection<ConfigWarning> warnings) =>
    Read(list, key, warnings, pattern => PathPattern.Parse(pattern).Invert());

  /// <summary>Reads a <c>branches</c> list.</summary>
  /// <param name="list">The list, if it is present.</param>
  /// <param name="key">Where the list is, for the warnings.</param>
  /// <param name="warnings">Receives what cannot be applied as written.</param>
  /// <returns>The patterns, those that cannot run included.</returns>
  internal static IReadOnlyList<PathPattern> ReadBranches(
    ConfigNode? list,
    string key,
    ICollection<ConfigWarning> warnings) =>
    Read(list, key, warnings, PathPattern.ParseBranch);

  private static List<PathPattern> Read(
    ConfigNode? list,
    string key,
    ICollection<ConfigWarning> warnings,
    Func<string, PathPattern> parse)
  {
    if (list is null || list.Kind is ConfigNodeKind.Null)
    {
      return [];
    }

    if (list.Kind is not ConfigNodeKind.List)
    {
      warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, key, list.AsText()));

      return [];
    }

    var patterns = new List<PathPattern>(list.Items.Count);

    for (var index = 0; index < list.Items.Count; index++)
    {
      var entryKey = string.Create(CultureInfo.InvariantCulture, $"{key}[{index}]");

      if (list.Items[index].AsText() is not { } text)
      {
        warnings.Add(new ConfigWarning(ConfigWarning.InvalidValue, entryKey));

        continue;
      }

      var pattern = parse(text);

      if (!pattern.IsSupported)
      {
        warnings.Add(new ConfigWarning(ConfigWarning.UnsupportedPattern, entryKey, text));
      }

      patterns.Add(pattern);
    }

    return patterns;
  }

  #endregion
}
