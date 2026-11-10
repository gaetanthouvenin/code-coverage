// ------------------------------------------------------------------------------------------------
// <copyright file="PatchCoverage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>The coverage of the lines a diff adds (ARCHITECTURE §7.3).</summary>
/// <param name="Totals">The totals of every added line that the head report can cover.</param>
/// <param name="Files">The files that hold such lines, and those whose diff the forge did not give, by path.</param>
public sealed record PatchCoverage(CoverageTotals Totals, IReadOnlyList<PatchFileCoverage> Files)
{
  #region Properties

  /// <summary>Gets the patch of a diff that adds nothing.</summary>
  public static PatchCoverage Empty { get; } = new(default, []);

  /// <summary>Gets the coverage of the patch, or <see langword="null" /> when the diff adds no line that can be covered.</summary>
  public decimal? Coverage => Totals.Coverage;

  /// <summary>Gets a value indicating whether the forge gave no patch for one file at least.</summary>
  public bool HasUnavailableDiff => Files.Any(static file => file.DiffUnavailable);

  #endregion
}