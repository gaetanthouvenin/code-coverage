// ------------------------------------------------------------------------------------------------
// <copyright file="CcKpiIconStyle.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>The background of the round icon of a <see cref="CcKpiCard" />.</summary>
public enum CcKpiIconStyle
{
  /// <summary>A soft gray round.</summary>
  Soft,

  /// <summary>A black (white in the dark theme) round.</summary>
  Ink,

  /// <summary>A round in the blue gradient.</summary>
  Gradient,
}
