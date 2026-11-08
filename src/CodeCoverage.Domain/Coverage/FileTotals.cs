// ------------------------------------------------------------------------------------------------
// <copyright file="FileTotals.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>The counters of one file of a report.</summary>
/// <param name="Path">The path of the file.</param>
/// <param name="Totals">The counters of the lines of the file.</param>
public readonly record struct FileTotals(string Path, CoverageTotals Totals);
