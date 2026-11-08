// ------------------------------------------------------------------------------------------------
// <copyright file="JsonListConverter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CodeCoverage.Infrastructure.Persistence.Conventions;

/// <summary>Stores a small list (<c>Flags</c>, <c>ParentShas</c>) as a JSON text column.</summary>
/// <typeparam name="T">The type of the items.</typeparam>
/// <remarks>
///   JSON text is neutral between the engines (no <c>jsonb</c>, no <c>OPENJSON</c>): the column is
///   read and written whole, never queried. Pair it with <see cref="Comparer" />, so that EF Core sees
///   a change of the items and not only a new list:
///   <code>builder.Property(u => u.Flags).HasConversion(new JsonListConverter&lt;string&gt;(), JsonListConverter&lt;string&gt;.Comparer);</code>
/// </remarks>
internal sealed class JsonListConverter<T> : ValueConverter<IReadOnlyList<T>, string>
{
  #region Fields

  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="JsonListConverter{T}" /> class.</summary>
  public JsonListConverter()
    : base(static items => Write(items), static json => Read(json))
  {
  }

  #endregion

  #region Properties

  /// <summary>Gets the comparer that compares two lists item by item, in order.</summary>
  /// <value>The comparer of the lists, which snapshots a copy of the items.</value>
  public static ValueComparer<IReadOnlyList<T>> Comparer { get; } = new(
    static (left, right) => ReferenceEquals(left, right)
                            || (left != null && right != null && left.SequenceEqual(right)),
    static items => items.Aggregate(0, static (hash, item) => HashCode.Combine(hash, item)),
    static items => items.ToArray()
  );

  #endregion

  #region Methods

  /// <summary>Writes the items as a JSON array.</summary>
  /// <param name="items">The items.</param>
  /// <returns>The JSON array.</returns>
  private static string Write(IReadOnlyList<T> items) => JsonSerializer.Serialize(items, SerializerOptions);

  /// <summary>Reads a JSON array.</summary>
  /// <param name="json">The JSON array.</param>
  /// <returns>The items; none for an empty text or a JSON <c>null</c>.</returns>
  private static IReadOnlyList<T> Read(string json) =>
    string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<T[]>(json, SerializerOptions) ?? [];

  #endregion
}
