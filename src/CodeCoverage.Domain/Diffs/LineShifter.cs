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

    return Shift(diff, lines, static line => line.Line, static (line, number) => line with { Line = number });
  }

  /// <summary>Follows methods through the diff of their file, as their first lines would be.</summary>
  /// <param name="diff">The diff from the commit of the methods to the target commit.</param>
  /// <param name="methods">The methods, numbered in the old file, in any order.</param>
  /// <returns>
  ///   The methods whose first line still exists, numbered in the new file, in ascending order: a
  ///   method whose first line is removed or modified is dropped with it.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="diff" /> or <paramref name="methods" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="diff" /> has no patch: its lines cannot be followed.
  /// </exception>
  public static IReadOnlyList<MethodCoverage> ShiftMethods(FileDiff diff, IEnumerable<MethodCoverage> methods)
  {
    ArgumentNullException.ThrowIfNull(diff);
    ArgumentNullException.ThrowIfNull(methods);

    return Shift(diff, methods, static method => method.Line, static (method, number) => new MethodCoverage(number, method.Hits));
  }

  /// <summary>Follows numbered items through the diff of their file.</summary>
  /// <typeparam name="T">The type of the items.</typeparam>
  /// <param name="diff">The diff.</param>
  /// <param name="items">The items, numbered in the old file, in any order.</param>
  /// <param name="lineOf">Reads the line of an item.</param>
  /// <param name="move">Gives an item its line in the new file.</param>
  /// <returns>The items whose line still exists, numbered in the new file, in ascending order.</returns>
  /// <exception cref="ArgumentException">
  ///   <paramref name="diff" /> has no patch: its lines cannot be followed.
  /// </exception>
  private static List<T> Shift<T>(FileDiff diff, IEnumerable<T> items, Func<T, int> lineOf, Func<T, int, T> move)
  {
    ArgumentException.ThrowIf(!diff.HasPatch, "A diff without a patch cannot shift lines.", nameof(diff));

    var removed = diff.RemovedLines;
    var added = diff.AddedLines;
    var removedBefore = 0;
    var addedBefore = 0;
    var shifted = new List<T>();

    foreach (var item in items.OrderBy(lineOf))
    {
      var line = lineOf(item);

      while (removedBefore < removed.Count && removed[removedBefore] < line)
      {
        removedBefore++;
      }

      if (removedBefore < removed.Count && removed[removedBefore] == line)
      {
        continue;
      }

      var rank = line - removedBefore;

      while (addedBefore < added.Count && added[addedBefore] <= rank + addedBefore)
      {
        addedBefore++;
      }

      shifted.Add(move(item, rank + addedBefore));
    }

    return shifted;
  }

  #endregion
}
