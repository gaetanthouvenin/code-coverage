// ------------------------------------------------------------------------------------------------
// <copyright file="CcChartSeries.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Ui.Models;

/// <summary>A series of <see cref="CcLineChart" />.</summary>
/// <param name="Label">Its label, written at the end of its line and in the table of values.</param>
/// <param name="Points">Its points, the oldest first.</param>
public sealed record CcChartSeries(string Label, IReadOnlyList<CcChartPoint> Points);
