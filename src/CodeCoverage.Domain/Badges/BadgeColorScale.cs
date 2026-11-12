// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeColorScale.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Badges;

/// <summary>
///   The colors of a coverage badge: 101 steps from red to bright green over a range, <c>[70, 100]</c> unless the YAML
///   of the repository names <c>coverage.range</c> (ARCHITECTURE §11.2).
/// </summary>
/// <remarks>
///   The steps go through <c>#e05d44 → #fe7d37 → #dfb317 → #a4a61d → #97CA00 → #4c1</c>, interpolated in RGB, twenty steps
///   between two stops. At or under the low end the badge is red; at or above the high end, or at 100, bright green;
///   in between the step is <c>(int)((coverage − low) / (high − low) × 100)</c>.
/// </remarks>
public static class BadgeColorScale
{
  #region Fields

  /// <summary>The low end of the range of a badge when the YAML names none, as for the badges of Codecov.</summary>
  public const decimal DefaultLow = 70m;

  /// <summary>The high end of the range of a badge when the YAML names none.</summary>
  public const decimal DefaultHigh = 100m;

  /// <summary>The color of the « unknown » badge.</summary>
  public const string UnknownColor = "#9f9f9f";

  /// <summary>The number of steps of the scale.</summary>
  public const int Steps = 101;

  private static readonly (int R, int G, int B)[] Stops =
  [
    (0xe0, 0x5d, 0x44), (0xfe, 0x7d, 0x37), (0xdf, 0xb3, 0x17), (0xa4, 0xa6, 0x1d), (0x97, 0xca, 0x00), (0x44, 0xcc, 0x11),
  ];

  private static readonly string[] Scale = [.. Enumerable.Range(0, Steps).Select(Interpolate)];

  #endregion

  #region Methods

  /// <summary>Gives the color of a coverage.</summary>
  /// <param name="coverage">The coverage in percent.</param>
  /// <param name="range">The range named by the YAML of the repository, or <see langword="null" /> for <c>[70, 100]</c>.</param>
  /// <returns>The color, as <c>#rrggbb</c>.</returns>
  public static string Color(decimal coverage, CoverageRange? range)
  {
    var low = range?.Low ?? DefaultLow;
    var high = range?.High ?? DefaultHigh;

    if (coverage >= 100m || coverage >= high)
    {
      return Scale[Steps - 1];
    }

    if (coverage <= low || high <= low)
    {
      return Scale[0];
    }

    var step = (int)((coverage - low) / (high - low) * 100m);

    return Scale[Math.Clamp(step, 0, Steps - 1)];
  }

  /// <summary>Builds the badge of a coverage.</summary>
  /// <param name="coverage">The coverage in percent, or <see langword="null" />.</param>
  /// <param name="range">The range named by the YAML of the repository, or <see langword="null" />.</param>
  /// <returns>The badge, unknown without a coverage.</returns>
  public static Badge Of(decimal? coverage, CoverageRange? range) =>
    coverage is { } value ? new Badge(value, Color(value, range)) : Badge.Unknown;

  /// <summary>Computes one step of the scale.</summary>
  /// <param name="step">The step, from 0 to 100.</param>
  /// <returns>The color, as <c>#rrggbb</c>.</returns>
  private static string Interpolate(int step)
  {
    var segment = Math.Min(step / 20, Stops.Length - 2);
    var t = (step - (segment * 20)) / 20d;
    var (r1, g1, b1) = Stops[segment];
    var (r2, g2, b2) = Stops[segment + 1];

    static int Mix(int from, int to, double t) => (int)Math.Round(from + ((to - from) * t), MidpointRounding.AwayFromZero);

    return string.Create(CultureInfo.InvariantCulture, $"#{Mix(r1, r2, t):x2}{Mix(g1, g2, t):x2}{Mix(b1, b2, t):x2}");
  }

  #endregion
}
