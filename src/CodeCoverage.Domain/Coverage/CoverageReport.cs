// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageReport.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>The coverage of a set of files.</summary>
public sealed class CoverageReport
{
  #region Fields

  private readonly FileCoverage[] _files;
  private readonly FileTotals[] _fileTotals;
  private readonly Dictionary<string, FileCoverage> _filesByPath;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CoverageReport" /> class.</summary>
  /// <param name="files">The files, sorted by path, each path appearing once.</param>
  private CoverageReport(FileCoverage[] files)
  {
    _files = files;
    _fileTotals = [.. files.Select(file => new FileTotals(file.Path, file.Totals))];
    _filesByPath = files.ToDictionary(file => file.Path, StringComparer.Ordinal);
    Totals = new CoverageTotals(
      files.Sum(file => file.Totals.Hits),
      files.Sum(file => file.Totals.Misses),
      files.Sum(file => file.Totals.Partials),
      files.Sum(file => file.Totals.Branches),
      files.Sum(file => file.Totals.Methods));
  }

  #endregion

  #region Properties

  /// <summary>Gets the files, in ordinal order of their paths.</summary>
  public IReadOnlyList<FileCoverage> Files => _files;

  /// <summary>Gets the counters of each file, in the order of <see cref="Files" />.</summary>
  public IReadOnlyList<FileTotals> FileTotals => _fileTotals;

  /// <summary>Gets the sum of the counters of the files.</summary>
  public CoverageTotals Totals { get; }

  #endregion

  #region Methods

  /// <summary>Creates a report.</summary>
  /// <param name="files">
  ///   The files, in any order. Those of one path are merged, and those without any line are
  ///   dropped, as in Codecov. Paths are compared exactly.
  /// </param>
  /// <returns>The report.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="files" /> is <see langword="null" />.
  /// </exception>
  public static CoverageReport Create(IEnumerable<FileCoverage> files)
  {
    ArgumentNullException.ThrowIfNull(files);

    FileCoverage[] merged =
    [
      .. files
        .Where(file => file.Lines.Count > 0)
        .GroupBy(file => file.Path, StringComparer.Ordinal)
        .Select(group => FileCoverage.Create(group.Key, group.SelectMany(file => file.Lines)))
        .OrderBy(file => file.Path, StringComparer.Ordinal),
    ];

    return new CoverageReport(merged);
  }

  /// <summary>Counts some lines of a file.</summary>
  /// <param name="path">The path of the file.</param>
  /// <param name="lines">The numbers of the lines; those the file does not cover are ignored.</param>
  /// <returns>The counters of these lines, all at zero when the report has no such file.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="path" /> or <paramref name="lines" /> is <see langword="null" />.
  /// </exception>
  public CoverageTotals TotalsOf(string path, IEnumerable<int> lines)
  {
    ArgumentNullException.ThrowIfNull(path);
    ArgumentNullException.ThrowIfNull(lines);

    return _filesByPath.TryGetValue(path, out var file) ? file.TotalsOf(lines) : default;
  }

  #endregion
}
