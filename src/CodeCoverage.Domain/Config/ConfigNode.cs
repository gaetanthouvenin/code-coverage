// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigNode.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>An immutable value of a <c>codecov.yml</c> file, independent of the YAML library.</summary>
/// <remarks>
///   A key that is absent and a key set to <see cref="Null" /> are two different things: the second one
///   replaces what a lower layer defines.
/// </remarks>
public sealed class ConfigNode
{
  #region Fields

  private readonly object? _scalar;

  private readonly Dictionary<string, ConfigNode>? _index;

  #endregion

  #region Ctors

  private ConfigNode(
    ConfigNodeKind kind,
    object? scalar = null,
    Dictionary<string, ConfigNode>? index = null,
    IReadOnlyList<(string Key, ConfigNode Value)>? entries = null,
    IReadOnlyList<ConfigNode>? items = null)
  {
    Kind = kind;
    _scalar = scalar;
    _index = index;
    Entries = entries ?? [];
    Items = items ?? [];
  }

  #endregion

  #region Properties

  /// <summary>Gets the explicit empty value.</summary>
  public static ConfigNode Null { get; } = new(ConfigNodeKind.Null);

  /// <summary>Gets what the node holds.</summary>
  public ConfigNodeKind Kind { get; }

  /// <summary>Gets the entries of a map, in the order of the file; empty for any other kind.</summary>
  public IReadOnlyList<(string Key, ConfigNode Value)> Entries { get; }

  /// <summary>Gets the items of a list; empty for any other kind.</summary>
  public IReadOnlyList<ConfigNode> Items { get; }

  #endregion

  #region Methods

  /// <summary>Creates a true or false value.</summary>
  /// <param name="value">The value.</param>
  /// <returns>The node.</returns>
  public static ConfigNode FromBoolean(bool value) => new(ConfigNodeKind.Boolean, value);

  /// <summary>Creates a number.</summary>
  /// <param name="value">The value.</param>
  /// <returns>The node.</returns>
  public static ConfigNode FromNumber(decimal value) => new(ConfigNodeKind.Number, value);

  /// <summary>Creates a text.</summary>
  /// <param name="value">The value.</param>
  /// <returns>The node.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="value" /> is null.</exception>
  public static ConfigNode FromText(string value)
  {
    ArgumentNullException.ThrowIfNull(value);

    return new ConfigNode(ConfigNodeKind.Text, value);
  }

  /// <summary>Creates a map.</summary>
  /// <param name="entries">The entries. A repeated key keeps its place and takes the last value.</param>
  /// <returns>The node.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="entries" /> is null.</exception>
  public static ConfigNode FromMap(params IEnumerable<(string Key, ConfigNode Value)> entries)
  {
    ArgumentNullException.ThrowIfNull(entries);

    var index = new Dictionary<string, ConfigNode>(StringComparer.Ordinal);
    var positions = new Dictionary<string, int>(StringComparer.Ordinal);
    var ordered = new List<(string Key, ConfigNode Value)>();

    foreach (var entry in entries)
    {
      if (positions.TryGetValue(entry.Key, out var position))
      {
        ordered[position] = entry;
      }
      else
      {
        positions[entry.Key] = ordered.Count;
        ordered.Add(entry);
      }

      index[entry.Key] = entry.Value;
    }

    return new ConfigNode(ConfigNodeKind.Map, index: index, entries: ordered);
  }

  /// <summary>Creates a list.</summary>
  /// <param name="items">The items.</param>
  /// <returns>The node.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="items" /> is null.</exception>
  public static ConfigNode FromList(params IEnumerable<ConfigNode> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    return new ConfigNode(ConfigNodeKind.List, items: [.. items]);
  }

  /// <summary>Reads a true or false value.</summary>
  /// <returns>The value, or <see langword="null" /> for any other kind.</returns>
  public bool? AsBoolean() => _scalar as bool?;

  /// <summary>Reads a number.</summary>
  /// <returns>The value, or <see langword="null" /> for any other kind.</returns>
  public decimal? AsNumber() => _scalar as decimal?;

  /// <summary>Reads a whole number that fits an integer.</summary>
  /// <returns>The value, or <see langword="null" /> for anything else.</returns>
  public int? AsInteger() =>
    _scalar is decimal value && decimal.Truncate(value) == value && value is >= int.MinValue and <= int.MaxValue
      ? (int)value
      : null;

  /// <summary>Reads a text.</summary>
  /// <returns>The value, or <see langword="null" /> for any other kind.</returns>
  public string? AsText() => _scalar as string;

  /// <summary>Walks down nested maps.</summary>
  /// <param name="path">The keys to follow.</param>
  /// <returns>
  ///   The node found, which can be <see cref="Null" />, or <see langword="null" /> if a key is absent
  ///   or a step is not a map.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
  public ConfigNode? Find(params IEnumerable<string> path)
  {
    ArgumentNullException.ThrowIfNull(path);

    var node = this;

    foreach (var key in path)
    {
      if (node._index is null || !node._index.TryGetValue(key, out var next))
      {
        return null;
      }

      node = next;
    }

    return node;
  }

  #endregion
}
