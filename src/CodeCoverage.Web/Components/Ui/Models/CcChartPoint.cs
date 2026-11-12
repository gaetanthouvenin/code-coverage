// ------------------------------------------------------------------------------------------------
// <copyright file="CcChartPoint.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>A point of a series of <see cref="CcLineChart" />.</summary>
/// <param name="At">When the value was measured.</param>
/// <param name="Value">The value, or <see langword="null" /> for a gap in the line.</param>
public sealed record CcChartPoint(DateTimeOffset At, double? Value);
