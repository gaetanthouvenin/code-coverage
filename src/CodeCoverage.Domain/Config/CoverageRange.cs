// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageRange.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

namespace CodeCoverage.Domain.Config;

/// <summary>The bounds of the color scale (<c>coverage.range</c>).</summary>
public sealed record CoverageRange
{
  #region Fields

  private const decimal Minimum = 0;

  private const decimal Maximum = 100;

  #endregion

  #region Ctors

  private CoverageRange(decimal low, decimal high)
  {
    Low = low;
    High = high;
  }

  #endregion

  #region Properties

  /// <summary>Gets the coverage under which the color is the lowest one.</summary>
  public decimal Low { get; }

  /// <summary>Gets the coverage from which the color is the highest one.</summary>
  public decimal High { get; }

  #endregion

  #region Methods

  /// <summary>Reads a range, as <c>CoverageRangeSchemaField</c> of Codecov does.</summary>
  /// <param name="node">A text such as <c>60..80</c> or <c>60...80</c>, or a list of two numbers.</param>
  /// <returns>
  ///   The range, or <see langword="null" /> if the value is malformed, out of 0 to 100 or inverted. A
  ///   list is sorted first, so only a text can be inverted.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="node" /> is null.</exception>
  public static CoverageRange? Read(ConfigNode node)
  {
    ArgumentNullException.ThrowIfNull(node);

    return node.Kind switch
    {
      ConfigNodeKind.List => FromList(node.Items),
      ConfigNodeKind.Text => FromText(node.AsText()!),
      var _ => null,
    };
  }

  private static CoverageRange? FromList(IReadOnlyList<ConfigNode> items)
  {
    if (items.Count != 2 || ToNumber(items[0]) is not { } first || ToNumber(items[1]) is not { } second)
    {
      return null;
    }

    return Create(Math.Min(first, second), Math.Max(first, second));
  }

  private static CoverageRange? FromText(string text)
  {
    if (text.Contains("....", StringComparison.Ordinal))
    {
      return null;
    }

    var parts = text.Split(text.Contains("...", StringComparison.Ordinal) ? "..." : "..");

    return parts.Length == 2 && ParseNumber(parts[0]) is { } low && ParseNumber(parts[1]) is { } high
      ? Create(low, high)
      : null;
  }

  private static decimal? ToNumber(ConfigNode node) =>
    node.AsNumber() ?? (node.AsText() is { } text ? ParseNumber(text) : null);

  private static decimal? ParseNumber(string text) =>
    decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
      ? value
      : null;

  private static CoverageRange? Create(decimal low, decimal high) =>
    low >= Minimum && high <= Maximum && low <= high ? new CoverageRange(low, high) : null;

  #endregion
}
