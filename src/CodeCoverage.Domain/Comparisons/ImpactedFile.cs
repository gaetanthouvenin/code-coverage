// ------------------------------------------------------------------------------------------------
// <copyright file="ImpactedFile.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>A file of the diff that one report at least covers, with its totals on each side.</summary>
/// <param name="Path">The path of the file in the head commit.</param>
/// <param name="PreviousPath">The path of the file in the base commit when the diff renames it, or <see langword="null" />.</param>
/// <param name="Status">How the diff changes the file.</param>
/// <param name="Base">The totals of the file in the base report, or <see langword="null" /> when that report does not hold it.</param>
/// <param name="Head">The totals of the file in the head report, or <see langword="null" /> when that report does not hold it.</param>
/// <param name="Patch">The totals of the lines the diff adds to the file.</param>
public sealed record ImpactedFile(
  string Path,
  string? PreviousPath,
  FileDiffStatus Status,
  CoverageTotals? Base,
  CoverageTotals? Head,
  CoverageTotals Patch)
{
  #region Properties

  /// <summary>Gets the change of coverage of the file, not rounded, or <see langword="null" /> when a side has no coverage.</summary>
  public decimal? Delta => ComparisonCalculator.Delta(Head, Base);

  /// <summary>Gets the number of added lines that are not fully covered.</summary>
  public int MissingPatchLines => Patch.Misses + Patch.Partials;

  #endregion
}