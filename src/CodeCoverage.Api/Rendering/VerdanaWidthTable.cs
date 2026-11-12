// ------------------------------------------------------------------------------------------------
// <copyright file="VerdanaWidthTable.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Frozen;

namespace CodeCoverage.Api.Rendering;

/// <summary>The advance widths of the printable ASCII characters in Verdana 11 px, to size the text of a badge.</summary>
/// <remarks>
///   The badge is drawn as text by the browser: the widths only size its two boxes. A character outside the table counts
///   as <see cref="FallbackWidth" />, so that no flag name can break the rendering.
/// </remarks>
internal static class VerdanaWidthTable
{
  #region Fields

  /// <summary>The width of a character the table does not hold, in pixels.</summary>
  public const double FallbackWidth = 7.0;

  // From ' ' (32) to '~' (126), in that order.
  private static readonly double[] AsciiWidths =
  [
    3.87, 4.33, 5.05, 9.0, 6.99, 11.84, 7.99, 2.95, 4.99, 4.99, 6.99, 9.0, 4.0, 4.99, 4.0, 4.99,
    6.99, 6.99, 6.99, 6.99, 6.99, 6.99, 6.99, 6.99, 6.99, 6.99, 4.99, 4.99, 9.0, 9.0, 9.0, 6.0,
    11.0, 7.52, 7.54, 7.68, 8.48, 6.96, 6.32, 8.53, 8.27, 4.63, 5.0, 7.62, 6.12, 9.27, 8.23, 8.66,
    6.63, 8.66, 7.65, 7.52, 6.78, 8.05, 7.52, 10.88, 7.54, 6.77, 7.54, 4.99, 4.99, 4.99, 9.0, 6.99,
    6.99, 6.61, 6.85, 5.73, 6.85, 6.55, 3.87, 6.85, 6.96, 3.02, 3.79, 6.51, 3.02, 10.7, 6.96, 6.68,
    6.85, 6.85, 4.69, 5.73, 4.33, 6.96, 6.51, 9.0, 6.51, 6.51, 5.78, 6.98, 4.99, 6.98, 9.0,
  ];

  private static readonly FrozenDictionary<char, double> Widths =
    AsciiWidths.Select(static (width, index) => KeyValuePair.Create((char)(' ' + index), width)).ToFrozenDictionary();

  #endregion

  #region Methods

  /// <summary>Measures a text.</summary>
  /// <param name="text">The text.</param>
  /// <returns>Its width in pixels, rounded up.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="text" /> is <see langword="null" />.</exception>
  public static int Measure(string text)
  {
    ArgumentNullException.ThrowIfNull(text);

    return (int)Math.Ceiling(text.Sum(static c => Widths.GetValueOrDefault(c, FallbackWidth)));
  }

  #endregion
}
