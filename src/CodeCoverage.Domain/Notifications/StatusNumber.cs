// ------------------------------------------------------------------------------------------------
// <copyright file="StatusNumber.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Notifications;

/// <summary>Writes the numbers of a status description as <c>round_number</c> of Codecov does.</summary>
/// <remarks>
///   Not the display of the comment and of the pages (<c>CoverageMath.Format</c>): a status never writes
///   <c>&lt;0.01</c> nor <c>ø</c>, it writes the rounded number, <c>0.00</c> included.
/// </remarks>
internal static class StatusNumber
{
  #region Methods

  /// <summary>Rounds a number to the precision of the configuration.</summary>
  /// <param name="value">The number.</param>
  /// <param name="format">The precision and the rounding.</param>
  /// <returns>The rounded number.</returns>
  public static decimal Round(decimal value, StatusFormat format) =>
    Math.Round(
      value,
      format.Precision,
      format.Rounding switch
      {
        RoundingMode.Down => MidpointRounding.ToNegativeInfinity,
        RoundingMode.Up => MidpointRounding.ToPositiveInfinity,
        var _ => MidpointRounding.ToEven,
      }
    );

  /// <summary>Writes a number with every decimal of the precision.</summary>
  /// <param name="value">The number.</param>
  /// <param name="format">The precision and the rounding.</param>
  /// <returns>The text, e.g. <c>85.50</c>.</returns>
  public static string Text(decimal value, StatusFormat format) => Fixed(Round(value, format), format);

  /// <summary>Writes a change with its sign, as the <c>:+</c> format of Python does.</summary>
  /// <param name="value">The change.</param>
  /// <param name="format">The precision and the rounding.</param>
  /// <returns>The text, e.g. <c>+0.25</c>, <c>-1.00</c> or <c>+0.00</c>; a drop that rounds to zero keeps its sign, <c>-0.00</c>.</returns>
  public static string Signed(decimal value, StatusFormat format) =>
    (value < 0 ? "-" : "+") + Fixed(Math.Abs(Round(value, format)), format);

  /// <summary>Writes a rounded number with a fixed number of decimals.</summary>
  /// <param name="rounded">The rounded number.</param>
  /// <param name="format">The precision.</param>
  /// <returns>The text.</returns>
  private static string Fixed(decimal rounded, StatusFormat format) =>
    rounded.ToString("F" + format.Precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

  #endregion
}