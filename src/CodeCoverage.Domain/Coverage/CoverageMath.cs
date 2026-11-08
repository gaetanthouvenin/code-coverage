// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageMath.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Coverage;

/// <summary>The coverage formulas and their display, identical to those of Codecov.</summary>
public static class CoverageMath
{
  #region Fields

  /// <summary>The text of a value that does not exist.</summary>
  public const string NullValue = "∅";

  /// <summary>The text of a change of exactly zero.</summary>
  public const string NoImpact = "ø";

  /// <summary>The largest number of decimals a <see cref="decimal" /> can hold.</summary>
  public const int MaxPrecision = 28;

  /// <summary>The format that gives a ratio its 5 decimals.</summary>
  private const string RatioFormat = "F5";

  #endregion

  #region Methods

  /// <summary>Computes a coverage in percent.</summary>
  /// <param name="hits">The number of lines hit.</param>
  /// <param name="lines">The number of coverable lines.</param>
  /// <returns>The coverage, with 5 decimals, or <see langword="null" /> without any line.</returns>
  /// <remarks>
  ///   A partial line counts in <paramref name="lines" /> and never in <paramref name="hits" />.
  ///   The result is the one of Python's <c>round(hits / lines * 100, 5)</c>, which rounds the
  ///   exact binary value of the double.
  ///   <see cref="Math.Round(double, int, MidpointRounding)" /> does not: it multiplies first, and
  ///   that product can land on a tie that the double is not (2 of 64000 lines must give 0.00313,
  ///   it gives 0.00312). Formatting the double rounds it like Python does.
  /// </remarks>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="hits" /> or <paramref name="lines" /> is negative, or
  ///   <paramref name="hits" /> is greater than <paramref name="lines" />.
  /// </exception>
  public static decimal? Ratio(int hits, int lines)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(hits);
    ArgumentOutOfRangeException.ThrowIfNegative(lines);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(hits, lines);

    if (lines == 0)
    {
      return null;
    }

    if (hits == lines)
    {
      return 100m;
    }

    if (hits == 0)
    {
      return 0m;
    }

    var percentage = (double)hits / lines * 100;
    var text = percentage.ToString(RatioFormat, CultureInfo.InvariantCulture);

    return decimal.Parse(text, CultureInfo.InvariantCulture);
  }

  /// <summary>Tells how a line counts in the totals.</summary>
  /// <param name="line">The line.</param>
  /// <returns>The state of the line.</returns>
  /// <remarks>The hits of a line are ignored as soon as it has branch data.</remarks>
  public static LineState Classify(LineCoverage line)
  {
    if (!line.IsBranch)
    {
      return line.Hits > 0 ? LineState.Hit : LineState.Miss;
    }

    if (line.Covered == line.Total)
    {
      return LineState.Hit;
    }

    return line.Covered == 0 ? LineState.Miss : LineState.Partial;
  }

  /// <summary>Formats a coverage for display.</summary>
  /// <param name="value">The coverage, or <see langword="null" />.</param>
  /// <param name="precision">The number of decimals.</param>
  /// <param name="rounding">How the value is rounded.</param>
  /// <returns>
  ///   The text, with an invariant decimal point; <see cref="NullValue" /> for
  ///   <see langword="null" />, and <c>&lt;0.01</c> (at 2 decimals) for a value that is not zero
  ///   and rounds to zero.
  /// </returns>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="precision" /> is negative or greater than <see cref="MaxPrecision" />.
  /// </exception>
  public static string Format(decimal? value, int precision, RoundingMode rounding)
  {
    ThrowIfInvalid(precision);

    return value is null ? NullValue : FormatNumber(value.Value, precision, rounding, false);
  }

  /// <summary>Formats a change of coverage for display, with its sign.</summary>
  /// <param name="value">The change, or <see langword="null" />.</param>
  /// <param name="precision">The number of decimals.</param>
  /// <param name="rounding">How the value is rounded.</param>
  /// <returns>
  ///   The text, with an invariant decimal point; <see cref="NullValue" /> for
  ///   <see langword="null" />, <see cref="NoImpact" /> for exactly zero, and <c>+&lt;0.01</c> or
  ///   <c>-&lt;0.01</c> (at 2 decimals) for a value that is not zero and rounds to zero.
  /// </returns>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="precision" /> is negative or greater than <see cref="MaxPrecision" />.
  /// </exception>
  public static string FormatDelta(decimal? value, int precision, RoundingMode rounding)
  {
    ThrowIfInvalid(precision);

    if (value is null)
    {
      return NullValue;
    }

    return value == 0 ? NoImpact : FormatNumber(value.Value, precision, rounding, true);
  }

  /// <summary>Formats a number that exists.</summary>
  /// <param name="value">The number.</param>
  /// <param name="precision">The number of decimals.</param>
  /// <param name="rounding">How the number is rounded.</param>
  /// <param name="plus">A value indicating whether a positive number shows its sign.</param>
  /// <returns>The text.</returns>
  private static string FormatNumber(decimal value, int precision, RoundingMode rounding, bool plus)
  {
    var rounded = Math.Round(value, precision, ToMidpointRounding(rounding));

    if (rounded == 0 && value != 0)
    {
      var minimum = new decimal(1, 0, 0, false, (byte)precision);
      var sign = value < 0 ? "-" : plus ? "+" : string.Empty;

      return string.Create(CultureInfo.InvariantCulture, $"{sign}<{minimum}");
    }

    var format = "F" + precision.ToString(CultureInfo.InvariantCulture);
    var text = rounded.ToString(format, CultureInfo.InvariantCulture);

    return plus && rounded > 0 ? "+" + text : text;
  }

  /// <summary>Gets the .NET strategy of a rounding mode.</summary>
  /// <param name="rounding">The rounding mode.</param>
  /// <returns>The strategy.</returns>
  private static MidpointRounding ToMidpointRounding(RoundingMode rounding)
  {
    return rounding switch
    {
      RoundingMode.Down => MidpointRounding.ToNegativeInfinity,
      RoundingMode.Up => MidpointRounding.ToPositiveInfinity,
      var _ => MidpointRounding.ToEven,
    };
  }

  /// <summary>Checks a number of decimals.</summary>
  /// <param name="precision">The number of decimals.</param>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="precision" /> is negative or greater than <see cref="MaxPrecision" />.
  /// </exception>
  private static void ThrowIfInvalid(int precision)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(precision);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(precision, MaxPrecision);
  }

  #endregion
}
