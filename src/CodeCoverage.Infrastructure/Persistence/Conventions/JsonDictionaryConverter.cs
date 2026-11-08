// ------------------------------------------------------------------------------------------------
// <copyright file="JsonDictionaryConverter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CodeCoverage.Infrastructure.Persistence.Conventions;

/// <summary>Stores a small text map (the <c>Details</c> of an audit entry) as a JSON object column.</summary>
/// <remarks>
///   Like <see cref="JsonListConverter{T}" />, the JSON text is neutral between the engines and the
///   column is read and written whole, never queried. Pair it with <see cref="Comparer" />:
///   <code>builder.Property(e => e.Details).HasConversion(new JsonDictionaryConverter(), JsonDictionaryConverter.Comparer);</code>
///   The keys are written as given (no camel casing) and read back ordered by key.
/// </remarks>
internal sealed class JsonDictionaryConverter : ValueConverter<IReadOnlyDictionary<string, string>, string>
{
  #region Fields

  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="JsonDictionaryConverter" /> class.</summary>
  public JsonDictionaryConverter()
    : base(static map => Write(map), static json => Read(json))
  {
  }

  #endregion

  #region Properties

  /// <summary>Gets the comparer that compares two maps entry by entry, whatever their order.</summary>
  /// <value>The comparer of the maps, which snapshots a copy of the entries.</value>
  public static ValueComparer<IReadOnlyDictionary<string, string>> Comparer { get; } = new(
    static (left, right) => AreEqual(left, right),
    static map => HashOf(map),
    static map => Copy(map)
  );

  #endregion

  #region Methods

  /// <summary>Compares two maps entry by entry, whatever their order.</summary>
  /// <param name="left">The first map.</param>
  /// <param name="right">The second map.</param>
  /// <returns><see langword="true" /> when both maps hold the same entries.</returns>
  private static bool AreEqual(IReadOnlyDictionary<string, string>? left, IReadOnlyDictionary<string, string>? right)
  {
    if (ReferenceEquals(left, right))
    {
      return true;
    }

    if (left is null || right is null || left.Count != right.Count)
    {
      return false;
    }

    foreach (var (key, value) in left)
    {
      if (!right.TryGetValue(key, out var other) || !string.Equals(value, other, StringComparison.Ordinal))
      {
        return false;
      }
    }

    return true;
  }

  /// <summary>Copies a map, so that the snapshot does not follow later changes of the original.</summary>
  /// <param name="map">The map.</param>
  /// <returns>A copy of the entries, ordered by key.</returns>
  private static SortedDictionary<string, string> Copy(IReadOnlyDictionary<string, string> map) =>
    new(map.ToDictionary(), StringComparer.Ordinal);

  /// <summary>Hashes a map, whatever the order of its entries.</summary>
  /// <param name="map">The map.</param>
  /// <returns>The hash code of the entries.</returns>
  private static int HashOf(IReadOnlyDictionary<string, string> map) =>
    map.OrderBy(static entry => entry.Key, StringComparer.Ordinal)
       .Aggregate(0, static (hash, entry) => HashCode.Combine(hash, entry.Key, entry.Value));

  /// <summary>Writes the map as a JSON object.</summary>
  /// <param name="map">The map.</param>
  /// <returns>The JSON object.</returns>
  private static string Write(IReadOnlyDictionary<string, string> map) =>
    JsonSerializer.Serialize(map, SerializerOptions);

  /// <summary>Reads a JSON object.</summary>
  /// <param name="json">The JSON object.</param>
  /// <returns>The entries, ordered by key; none for an empty text or a JSON <c>null</c>.</returns>
  private static SortedDictionary<string, string> Read(string json) =>
    new(
      string.IsNullOrWhiteSpace(json)
        ? []
        : JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions) ?? [],
      StringComparer.Ordinal
    );

  #endregion
}
