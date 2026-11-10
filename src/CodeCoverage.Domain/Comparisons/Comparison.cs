// ------------------------------------------------------------------------------------------------
// <copyright file="Comparison.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>A head report compared with a base report (ARCHITECTURE §7.2 and §7.3).</summary>
/// <param name="Head">The totals of the head report.</param>
/// <param name="Base">The totals of the base report, or <see langword="null" /> without a base.</param>
/// <param name="Patch">The coverage of the lines the patch diff adds.</param>
/// <param name="Files">The files of the diff that a report covers, by path.</param>
/// <param name="Flags">The comparison of each flag, by flag.</param>
/// <param name="IndirectChanges">The changes of coverage outside the comparison diff, by path; none without a base.</param>
/// <param name="DiffIncomplete">Whether a diff of the comparison lacks files: the forge truncated it or could not give it.</param>
public sealed record Comparison(
  CoverageTotals Head,
  CoverageTotals? Base,
  PatchCoverage Patch,
  IReadOnlyList<ImpactedFile> Files,
  IReadOnlyList<FlagComparison> Flags,
  IReadOnlyList<IndirectChange> IndirectChanges,
  bool DiffIncomplete)
{
  #region Properties

  /// <summary>Gets the change of coverage of the project, not rounded, or <see langword="null" /> when a side has no coverage.</summary>
  public decimal? Delta => ComparisonCalculator.Delta(Head, Base);

  #endregion
}