// ------------------------------------------------------------------------------------------------
// <copyright file="FlagComparison.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>The comparison of one flag between the base and the head.</summary>
/// <param name="Flag">The flag; case-sensitive.</param>
/// <param name="Base">The totals of the flag in the base report, or <see langword="null" /> when no session of the base carries it.</param>
/// <param name="Head">The totals of the flag in the head report, or <see langword="null" /> when no session of the head carries it.</param>
/// <param name="Patch">The coverage, by the sessions of the flag, of the lines the diff adds.</param>
/// <param name="CarriedForward">Whether every session of the flag in the head was carried forward from an ancestor.</param>
public sealed record FlagComparison(string Flag, CoverageTotals? Base, CoverageTotals? Head, PatchCoverage Patch, bool CarriedForward)
{
  #region Properties

  /// <summary>Gets the change of coverage of the flag, not rounded, or <see langword="null" /> when a side has no coverage.</summary>
  public decimal? Delta => ComparisonCalculator.Delta(Head, Base);

  #endregion
}