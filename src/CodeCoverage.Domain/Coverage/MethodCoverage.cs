// ------------------------------------------------------------------------------------------------
// <copyright file="MethodCoverage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>A method of a file: the line where it starts and the number of times it ran.</summary>
/// <remarks>
///   Methods travel apart from the lines (ADR-016): they never change <see cref="LineCoverage.IsMethod" />
///   nor any counter of <see cref="CoverageTotals" />, which keep the values of Codecov.
/// </remarks>
public readonly record struct MethodCoverage
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="MethodCoverage" /> struct.</summary>
  /// <param name="line">The first line of the method, from 1.</param>
  /// <param name="hits">The number of times the method ran.</param>
  /// <exception cref="ArgumentOutOfRangeException">
  ///   <paramref name="line" /> is lower than 1, or <paramref name="hits" /> is negative.
  /// </exception>
  public MethodCoverage(int line, long hits)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
    ArgumentOutOfRangeException.ThrowIfNegative(hits);

    Line = line;
    Hits = hits;
  }

  #endregion

  #region Properties

  /// <summary>Gets the first line of the method.</summary>
  public int Line { get; }

  /// <summary>Gets the number of times the method ran.</summary>
  public long Hits { get; }

  /// <summary>Gets a value indicating whether the method ran at least once.</summary>
  public bool IsExecuted => Hits > 0;

  #endregion
}
