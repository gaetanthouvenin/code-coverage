// ------------------------------------------------------------------------------------------------
// <copyright file="FileTotals.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>The counters of one file of a report.</summary>
/// <param name="Path">The path of the file.</param>
/// <param name="Totals">The counters of the lines of the file, those of Codecov.</param>
/// <param name="Detail">
///   The lines, branches and methods of the file, or <see langword="null" /> when the source of the
///   counters does not keep them (ADR-016).
/// </param>
public readonly record struct FileTotals(string Path, CoverageTotals Totals, CoverageDetail? Detail = null);
