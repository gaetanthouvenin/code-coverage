// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigMerger.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>Merges the layers of a configuration, as <c>merge_yamls</c> of Codecov does.</summary>
public static class ConfigMerger
{
  #region Methods

  /// <summary>Applies a layer over a lower one.</summary>
  /// <param name="lower">The lower layer.</param>
  /// <param name="upper">The layer that wins.</param>
  /// <returns>
  ///   The two maps merged key by key, the keys of <paramref name="lower" /> first; if either side is
  ///   not a map, <paramref name="upper" />. A list or a <see cref="ConfigNode.Null" /> therefore
  ///   replaces what is below it.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="lower" /> or <paramref name="upper" /> is null.
  /// </exception>
  public static ConfigNode Merge(ConfigNode lower, ConfigNode upper)
  {
    ArgumentNullException.ThrowIfNull(lower);
    ArgumentNullException.ThrowIfNull(upper);

    if (lower.Kind is not ConfigNodeKind.Map || upper.Kind is not ConfigNodeKind.Map)
    {
      return upper;
    }

    var merged = new List<(string Key, ConfigNode Value)>(lower.Entries.Count + upper.Entries.Count);

    foreach (var (key, value) in lower.Entries)
    {
      merged.Add((key, upper.Find(key) is { } replacement ? Merge(value, replacement) : value));
    }

    foreach (var (key, value) in upper.Entries)
    {
      if (lower.Find(key) is null)
      {
        merged.Add((key, value));
      }
    }

    return ConfigNode.FromMap(merged);
  }

  #endregion
}
