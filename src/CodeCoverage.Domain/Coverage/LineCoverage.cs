// ------------------------------------------------------------------------------------------------
// <copyright file="LineCoverage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>The coverage of one line of a file.</summary>
/// <param name="Line">The number of the line, from 1.</param>
/// <param name="Hits">How many times the line was executed.</param>
/// <param name="Covered">The number of covered branches.</param>
/// <param name="Total">The number of branches; 0 when the line has no branch data.</param>
/// <param name="IsMethod">A value indicating whether the line declares a method.</param>
/// <param name="MissingBranches">
///   The identifiers of the branches not covered, or <see langword="null" /> when the report does
///   not name them.
/// </param>
/// <remarks>
///   The generated equality compares <paramref name="MissingBranches" /> by reference: two lines
///   with the same missing branches in two different arrays are not equal.
/// </remarks>
public readonly record struct LineCoverage(
  int Line,
  long Hits,
  int Covered,
  int Total,
  bool IsMethod,
  string[]? MissingBranches)
{
  #region Properties

  /// <summary>Gets a value indicating whether the line has branch data.</summary>
  public bool IsBranch => Total > 0;

  /// <summary>
  ///   Gets what the line is; as in Codecov, the method flag wins over branch data.
  /// </summary>
  public LineKind Kind
  {
    get
    {
      if (IsMethod)
      {
        return LineKind.Method;
      }

      return IsBranch ? LineKind.Branch : LineKind.Line;
    }
  }

  #endregion
}
