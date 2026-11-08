// ------------------------------------------------------------------------------------------------
// <copyright file="FileCoverage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>The coverable lines of one file.</summary>
public sealed class FileCoverage
{
  #region Fields

  private readonly LineCoverage[] _lines;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="FileCoverage" /> class.</summary>
  /// <param name="path">The path of the file.</param>
  /// <param name="lines">The lines, in ascending order and without duplicates.</param>
  private FileCoverage(string path, LineCoverage[] lines)
  {
    Path = path;
    _lines = lines;
    Totals = Count(lines);
  }

  #endregion

  #region Properties

  /// <summary>Gets the path of the file.</summary>
  public string Path { get; }

  /// <summary>Gets the lines, in ascending order, each number appearing once.</summary>
  public IReadOnlyList<LineCoverage> Lines => _lines;

  /// <summary>Gets the counters of all the lines.</summary>
  public CoverageTotals Totals { get; }

  #endregion

  #region Methods

  /// <summary>Creates the coverage of a file.</summary>
  /// <param name="path">The path of the file.</param>
  /// <param name="lines">The lines, in any order; two values of one line are merged.</param>
  /// <returns>The coverage of the file.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="path" /> or <paramref name="lines" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException"><paramref name="path" /> is blank.</exception>
  /// <exception cref="ArgumentOutOfRangeException">A line number is lower than 1.</exception>
  public static FileCoverage Create(string path, IEnumerable<LineCoverage> lines)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);
    ArgumentNullException.ThrowIfNull(lines);

    var merged = new Dictionary<int, LineCoverage>();

    foreach (var line in lines)
    {
      ArgumentOutOfRangeException.ThrowIfLessThan(line.Line, 1, nameof(lines));

      merged[line.Line] = merged.TryGetValue(line.Line, out var existing)
        ? ReportMerger.MergeLine(existing, line)
        : line;
    }

    return new FileCoverage(path, [.. merged.Values.OrderBy(line => line.Line)]);
  }

  /// <summary>Counts some lines of the file.</summary>
  /// <param name="lines">The numbers of the lines; those the file does not cover are ignored.</param>
  /// <returns>The counters of these lines.</returns>
  internal CoverageTotals TotalsOf(IEnumerable<int> lines)
  {
    var wanted = lines.ToHashSet();

    return Count(_lines.Where(line => wanted.Contains(line.Line)));
  }

  /// <summary>Counts lines by state and by kind.</summary>
  /// <param name="lines">The lines.</param>
  /// <returns>The counters.</returns>
  /// <remarks>A line counts in one kind only, whatever its state, as in Codecov.</remarks>
  private static CoverageTotals Count(IEnumerable<LineCoverage> lines)
  {
    var hits = 0;
    var misses = 0;
    var partials = 0;
    var branches = 0;
    var methods = 0;

    foreach (var line in lines)
    {
      switch (CoverageMath.Classify(line))
      {
        case LineState.Hit:
          hits++;
          break;
        case LineState.Miss:
          misses++;
          break;
        case LineState.Partial:
          partials++;
          break;
      }

      switch (line.Kind)
      {
        case LineKind.Branch:
          branches++;
          break;
        case LineKind.Method:
          methods++;
          break;
      }
    }

    return new CoverageTotals(hits, misses, partials, branches, methods);
  }

  #endregion
}
