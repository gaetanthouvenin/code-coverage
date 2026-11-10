// ------------------------------------------------------------------------------------------------
// <copyright file="PatchFileCoverage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>The coverage of the lines a diff adds to one file.</summary>
/// <param name="Path">The path of the file in the head commit.</param>
/// <param name="Totals">The totals of the added lines that the head report can cover.</param>
/// <param name="DiffUnavailable">
///   Whether the forge gave no patch for the file: its added lines are unknown, which is not the same as none.
/// </param>
public sealed record PatchFileCoverage(string Path, CoverageTotals Totals, bool DiffUnavailable);