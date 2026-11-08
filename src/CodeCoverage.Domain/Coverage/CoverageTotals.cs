// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageTotals.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>The counters of a set of lines, and the coverage they give.</summary>
/// <remarks>
///   <see cref="Branches" /> and <see cref="Methods" /> are informative: they never enter the
///   coverage.
/// </remarks>
public readonly record struct CoverageTotals
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CoverageTotals" /> struct.</summary>
  /// <param name="hits">The number of lines in the <see cref="LineState.Hit" /> state.</param>
  /// <param name="misses">The number of lines in the <see cref="LineState.Miss" /> state.</param>
  /// <param name="partials">
  ///   The number of lines in the <see cref="LineState.Partial" /> state.
  /// </param>
  /// <param name="branches">The number of lines of the <see cref="LineKind.Branch" /> kind.</param>
  /// <param name="methods">The number of lines of the <see cref="LineKind.Method" /> kind.</param>
  /// <exception cref="ArgumentOutOfRangeException">A counter is negative.</exception>
  /// <exception cref="OverflowException">
  ///   The number of coverable lines exceeds an integer.
  /// </exception>
  public CoverageTotals(int hits, int misses, int partials, int branches, int methods)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(hits);
    ArgumentOutOfRangeException.ThrowIfNegative(misses);
    ArgumentOutOfRangeException.ThrowIfNegative(partials);
    ArgumentOutOfRangeException.ThrowIfNegative(branches);
    ArgumentOutOfRangeException.ThrowIfNegative(methods);

    Hits = hits;
    Misses = misses;
    Partials = partials;
    Branches = branches;
    Methods = methods;
    Lines = checked(hits + misses + partials);
  }

  #endregion

  #region Properties

  /// <summary>Gets the number of lines in the <see cref="LineState.Hit" /> state.</summary>
  public int Hits { get; }

  /// <summary>Gets the number of lines in the <see cref="LineState.Miss" /> state.</summary>
  public int Misses { get; }

  /// <summary>Gets the number of lines in the <see cref="LineState.Partial" /> state.</summary>
  public int Partials { get; }

  /// <summary>Gets the number of lines of the <see cref="LineKind.Branch" /> kind.</summary>
  public int Branches { get; }

  /// <summary>Gets the number of lines of the <see cref="LineKind.Method" /> kind.</summary>
  public int Methods { get; }

  /// <summary>Gets the number of coverable lines.</summary>
  public int Lines { get; }

  /// <summary>
  ///   Gets the coverage in percent, with 5 decimals, or <see langword="null" /> without any
  ///   coverable line.
  /// </summary>
  public decimal? Coverage => CoverageMath.Ratio(Hits, Lines);

  #endregion
}
