// ------------------------------------------------------------------------------------------------
// <copyright file="ChangeDetector.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>Finds the changes of coverage outside the diff, line by line, after <c>get_changes</c> of Codecov. Pure.</summary>
public static class ChangeDetector
{
  #region Methods

  /// <summary>Detects the indirect changes between two reports.</summary>
  /// <param name="base">The base report.</param>
  /// <param name="head">The head report.</param>
  /// <param name="diff">The comparison diff, from the base to the head.</param>
  /// <returns>The changes, by path.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <remarks>
  ///   <para>
  ///     Every file of the diff is left out, under its path and under its previous path, whatever it
  ///     changes: its lines moved, and its coverage is the business of the patch.
  ///   </para>
  ///   <para>
  ///     A file outside the diff has the same line numbers in both commits: the state of each line (hit,
  ///     missed, partially hit, or absent) is compared directly. A line that stays in the same state is
  ///     not a change, however many times it is hit.
  ///   </para>
  /// </remarks>
  public static IReadOnlyList<IndirectChange> Detect(CoverageReport @base, CoverageReport head, DiffSet diff)
  {
    ArgumentNullException.ThrowIfNull(@base);
    ArgumentNullException.ThrowIfNull(head);
    ArgumentNullException.ThrowIfNull(diff);

    var inDiff = new HashSet<string>(StringComparer.Ordinal);

    foreach (var file in diff.Files)
    {
      inDiff.Add(file.Path);

      if (file.PreviousPath is { } previous)
      {
        inDiff.Add(previous);
      }
    }

    var baseFiles = @base.Files.Where(f => !inDiff.Contains(f.Path)).ToDictionary(static f => f.Path, StringComparer.Ordinal);
    var changes = new List<IndirectChange>();

    foreach (var headFile in head.Files.Where(f => !inDiff.Contains(f.Path)))
    {
      if (!baseFiles.Remove(headFile.Path, out var baseFile))
      {
        var totals = headFile.Totals;

        changes.Add(new IndirectChange(headFile.Path, IndirectChangeKind.Appeared, totals.Hits, totals.Misses, totals.Partials, null));

        continue;
      }

      if (Compare(baseFile, headFile) is { } change)
      {
        changes.Add(change);
      }
    }

    foreach (var gone in baseFiles.Values)
    {
      var totals = gone.Totals;

      changes.Add(new IndirectChange(gone.Path, IndirectChangeKind.Disappeared, -totals.Hits, -totals.Misses, -totals.Partials, null));
    }

    changes.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));

    return changes;
  }

  /// <summary>Compares the lines of a file that both reports hold.</summary>
  /// <param name="baseFile">The file in the base report.</param>
  /// <param name="headFile">The file in the head report.</param>
  /// <returns>The change, or <see langword="null" /> when every line is in the same state.</returns>
  private static IndirectChange? Compare(FileCoverage baseFile, FileCoverage headFile)
  {
    var before = baseFile.Lines.ToDictionary(static line => line.Line, CoverageMath.Classify);
    int hits = 0, misses = 0, partials = 0;
    var changed = false;

    foreach (var line in headFile.Lines)
    {
      var state = CoverageMath.Classify(line);

      if (before.Remove(line.Line, out var previous))
      {
        if (previous == state)
        {
          continue;
        }

        Count(previous, -1, ref hits, ref misses, ref partials);
      }

      Count(state, 1, ref hits, ref misses, ref partials);
      changed = true;
    }

    // What is left was covered in the base and is no longer a line of the head report.
    foreach (var previous in before.Values)
    {
      Count(previous, -1, ref hits, ref misses, ref partials);
      changed = true;
    }

    return changed
             ? new IndirectChange(
               headFile.Path,
               IndirectChangeKind.Changed,
               hits,
               misses,
               partials,
               ComparisonCalculator.Delta(headFile.Totals, baseFile.Totals)
             )
             : null;
  }

  /// <summary>Adds a line to the count of its state.</summary>
  /// <param name="state">The state of the line.</param>
  /// <param name="sign">1 for a line of the head, -1 for a line of the base.</param>
  /// <param name="hits">The count of the lines hit.</param>
  /// <param name="misses">The count of the lines missed.</param>
  /// <param name="partials">The count of the lines partially hit.</param>
  private static void Count(LineState state, int sign, ref int hits, ref int misses, ref int partials)
  {
    switch (state)
    {
      case LineState.Hit:
        hits += sign;

        break;
      case LineState.Miss:
        misses += sign;

        break;
      default:
        partials += sign;

        break;
    }
  }

  #endregion
}