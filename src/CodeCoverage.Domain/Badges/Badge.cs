// ------------------------------------------------------------------------------------------------
// <copyright file="Badge.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Badges;

/// <summary>The value and the color of a coverage badge.</summary>
/// <param name="Coverage">The coverage in percent, or <see langword="null" /> for the grey « unknown » badge.</param>
/// <param name="Color">The color, as <c>#rrggbb</c> or <c>#rgb</c>.</param>
/// <remarks>The label and the drawing belong to the rendering (M3.15); this is what a query answers.</remarks>
public sealed record Badge(decimal? Coverage, string Color)
{
  #region Properties

  /// <summary>Gets the grey « unknown » badge: an unknown repository, branch, flag or token gives it, never an error.</summary>
  public static Badge Unknown { get; } = new(null, BadgeColorScale.UnknownColor);

  /// <summary>Gets a value indicating whether the badge shows no value.</summary>
  public bool IsUnknown => Coverage is null;

  #endregion
}
