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
///   Two lines are equal when <paramref name="MissingBranches" /> hold the same identifiers in the
///   same order. <see langword="null" /> is not the same as an empty array: the first says the
///   report does not name the branches, the second that none is missing.
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

  #region Methods

  /// <summary>Tells whether another line has the same content.</summary>
  /// <param name="other">The other line.</param>
  /// <returns>
  ///   <see langword="true" /> when every field is equal, the missing branches being compared by
  ///   content.
  /// </returns>
  public bool Equals(LineCoverage other)
  {
    return Line == other.Line
      && Hits == other.Hits
      && Covered == other.Covered
      && Total == other.Total
      && IsMethod == other.IsMethod
      && HasSameMissingBranches(other);
  }

  /// <inheritdoc />
  public override int GetHashCode()
  {
    var hash = new HashCode();

    hash.Add(Line);
    hash.Add(Hits);
    hash.Add(Covered);
    hash.Add(Total);
    hash.Add(IsMethod);
    hash.Add(MissingBranches?.Length ?? -1);

    foreach (var branch in MissingBranches ?? [])
    {
      hash.Add(branch, StringComparer.Ordinal);
    }

    return hash.ToHashCode();
  }

  /// <summary>Compares the missing branches with those of another line.</summary>
  /// <param name="other">The other line.</param>
  /// <returns><see langword="true" /> when they have the same content.</returns>
  private bool HasSameMissingBranches(LineCoverage other)
  {
    if (MissingBranches is null || other.MissingBranches is null)
    {
      return MissingBranches is null && other.MissingBranches is null;
    }

    return MissingBranches.SequenceEqual(other.MissingBranches, StringComparer.Ordinal);
  }

  #endregion
}
