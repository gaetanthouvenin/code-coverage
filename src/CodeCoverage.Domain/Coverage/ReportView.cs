// ------------------------------------------------------------------------------------------------
// <copyright file="ReportView.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Coverage;

/// <summary>The coverage of a commit restricted to some flags and some paths, computed from its sessions.</summary>
public sealed class ReportView
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ReportView" /> class.</summary>
  /// <param name="report">The coverage of the view.</param>
  private ReportView(CoverageReport report)
  {
    Report = report;
  }

  #endregion

  #region Properties

  /// <summary>Gets the coverage of the view.</summary>
  public CoverageReport Report { get; }

  /// <summary>Gets the counters of the view.</summary>
  public CoverageTotals Totals => Report.Totals;

  #endregion

  #region Methods

  /// <summary>Builds a view from the sessions of a commit.</summary>
  /// <param name="sessions">The sessions, in any order.</param>
  /// <param name="flags">The flags of the view; none gives the merged report of the commit.</param>
  /// <param name="paths">The paths of the view; a set without any pattern keeps every file.</param>
  /// <returns>The view.</returns>
  /// <remarks>
  ///   Without flag, the sessions that do not join the merged report are left out. With flags, every
  ///   session carrying one of them counts, joined or not: <c>joined: false</c> only keeps a flag out
  ///   of the project.
  /// </remarks>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static ReportView Build(
    IEnumerable<SessionReport> sessions,
    IReadOnlyCollection<string> flags,
    PathPatternSet paths)
  {
    ArgumentNullException.ThrowIfNull(sessions);
    ArgumentNullException.ThrowIfNull(flags);
    ArgumentNullException.ThrowIfNull(paths);

    var selected = sessions.Where(session => IsSelected(session, flags));
    var merged = ReportMerger.Merge(selected.Select(session => session.Report));

    return new ReportView(CoverageReport.Create(merged.Files.Where(file => paths.Includes(file.Path))));
  }

  /// <summary>Counts some lines of a file of the view.</summary>
  /// <param name="path">The path of the file.</param>
  /// <param name="lines">The numbers of the lines; those the file does not cover are ignored.</param>
  /// <returns>The counters of these lines, all at zero when the view has no such file.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="path" /> or <paramref name="lines" /> is <see langword="null" />.
  /// </exception>
  public CoverageTotals TotalsOf(string path, IEnumerable<int> lines)
  {
    ArgumentNullException.ThrowIfNull(path);
    ArgumentNullException.ThrowIfNull(lines);

    return Report.TotalsOf(path, lines);
  }

  /// <summary>Tells whether a session belongs to a view.</summary>
  /// <param name="session">The session.</param>
  /// <param name="flags">The flags of the view.</param>
  /// <returns><see langword="true" /> when its coverage enters the view.</returns>
  private static bool IsSelected(SessionReport session, IReadOnlyCollection<string> flags)
  {
    return flags.Count == 0
      ? session.Joined
      : session.Flags.Any(flag => flags.Contains(flag, StringComparer.Ordinal));
  }

  #endregion
}
