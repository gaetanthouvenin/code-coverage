// ------------------------------------------------------------------------------------------------
// <copyright file="LineShifter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Diffs;

/// <summary>Moves the coverage of a commit onto the lines of a later commit.</summary>
public static class LineShifter
{
  #region Methods

  /// <summary>Follows lines through the diff of their file.</summary>
  /// <param name="diff">The diff from the commit of the lines to the target commit.</param>
  /// <param name="lines">The lines, numbered in the old file, in any order.</param>
  /// <returns>
  ///   The lines that still exist, numbered in the new file, in ascending order. A removed line is
  ///   dropped, and so is a modified one, as in Codecov (<c>shift_lines_by_diff</c>).
  /// </returns>
  /// <remarks>
  ///   Lines that a diff does not remove keep their relative order, and they are exactly the lines
  ///   of the new file that it does not add: the n-th remaining old line is the n-th line of the new
  ///   file that is not an added one.
  /// </remarks>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="diff" /> or <paramref name="lines" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="diff" /> has no patch: its lines cannot be followed.
  /// </exception>
  public static IReadOnlyList<LineCoverage> Shift(FileDiff diff, IEnumerable<LineCoverage> lines)
  {
    ArgumentNullException.ThrowIfNull(diff);
    ArgumentNullException.ThrowIfNull(lines);
    ArgumentException.ThrowIf(!diff.HasPatch, "A diff without a patch cannot shift lines.", nameof(diff));

    var removed = diff.RemovedLines;
    var added = diff.AddedLines;
    var removedBefore = 0;
    var addedBefore = 0;
    var shifted = new List<LineCoverage>();

    foreach (var line in lines.OrderBy(line => line.Line))
    {
      while (removedBefore < removed.Count && removed[removedBefore] < line.Line)
      {
        removedBefore++;
      }

      if (removedBefore < removed.Count && removed[removedBefore] == line.Line)
      {
        continue;
      }

      var rank = line.Line - removedBefore;

      while (addedBefore < added.Count && added[addedBefore] <= rank + addedBefore)
      {
        addedBefore++;
      }

      shifted.Add(line with { Line = rank + addedBefore });
    }

    return shifted;
  }

  #endregion
}
