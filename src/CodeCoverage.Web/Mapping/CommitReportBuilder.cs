// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReportBuilder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Mapping;

/// <summary>
///   Builds the detailed report of a commit from the counters of its files: the coverage of Codecov first, then the
///   executed lines, the branches and the methods of ADR-016, each written as the repository writes a coverage.
/// </summary>
internal static class CommitReportBuilder
{
  #region Methods

  /// <summary>Builds the report.</summary>
  /// <param name="shortSha">The short SHA of the commit.</param>
  /// <param name="branch">The branch of the commit, or <see langword="null" />.</param>
  /// <param name="flags">The flags of the commit.</param>
  /// <param name="isCompacted">A value indicating whether the commit was compacted.</param>
  /// <param name="files">The counters of the files, in any order; none for a compacted commit.</param>
  /// <param name="target">The fixed target of the project status, in percent, or <see langword="null" />.</param>
  /// <param name="precision">The number of decimals of a coverage.</param>
  /// <param name="rounding">How a coverage is rounded.</param>
  /// <returns>
  ///   The report, its files in ordinal path order; without the detail of one file, no file shows a detail, since the
  ///   index of the commit was written before it was stored.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="flags" /> or <paramref name="files" /> is <see langword="null" />.</exception>
  public static CommitReportViewModel Build(
    string shortSha,
    string? branch,
    IReadOnlyList<string> flags,
    bool isCompacted,
    IReadOnlyList<FileTotals> files,
    decimal? target,
    int precision,
    RoundingMode rounding)
  {
    ArgumentNullException.ThrowIfNull(flags);
    ArgumentNullException.ThrowIfNull(files);

    var hasDetail = files.All(static f => f.Detail is not null);
    var (hits, misses, partials) = (0L, 0L, 0L);
    var detail = new CoverageDetail(0, 0, 0, 0, 0, 0);

    foreach (var file in files)
    {
      (hits, misses, partials) = (hits + file.Totals.Hits, misses + file.Totals.Misses, partials + file.Totals.Partials);
      detail = file.Detail is { } fileDetail ? detail.Add(fileDetail) : detail;
    }

    var totals = new CoverageTotals(Saturate(hits), Saturate(misses), Saturate(partials), 0, 0);

    return new CommitReportViewModel(
      shortSha,
      branch,
      flags,
      isCompacted,
      hasDetail,
      target,
      target is { } value ? DisplayFormatter.Percent(value, precision, rounding) : null,
      ToRow(string.Empty, totals, hasDetail ? detail : null, precision, rounding),
      [
        .. files.OrderBy(static f => f.Path, StringComparer.Ordinal)
                .Select(f => ToRow(f.Path, f.Totals, hasDetail ? f.Detail : null, precision, rounding)),
      ]);
  }

  /// <summary>Reads the fixed target of the project status, the one the filter « below the target » compares with.</summary>
  /// <param name="config">The configuration of the repository.</param>
  /// <returns>The target of the first project status that has a percentage, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="config" /> is <see langword="null" />.</exception>
  /// <remarks>A target <c>auto</c> follows the base of each commit: it gives no fixed figure to compare a file with.</remarks>
  public static decimal? ProjectTarget(CoverageConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    return config.BuildStatusRules([]).Rules.FirstOrDefault(static r => r.Type is StatusType.Project && !r.Target.IsAuto)?.Target.Percentage;
  }

  private static CommitReportRowViewModel ToRow(string path, CoverageTotals totals, CoverageDetail? detail, int precision, RoundingMode rounding) =>
    new(
      path,
      totals.Hits,
      totals.Partials,
      totals.Misses,
      totals.Coverage,
      DisplayFormatter.Percent(totals.Coverage, precision, rounding),
      detail is { } lines ? ToCount(lines.ExecutedLines, lines.CoverableLines, lines.LineRatio, precision, rounding) : null,
      detail is { } branches ? ToCount(branches.CoveredBranches, branches.TotalBranches, branches.BranchRatio, precision, rounding) : null,
      detail is { CoveredMethods: { } covered, TotalMethods: { } total } methods
        ? ToCount(covered, total, methods.MethodRatio, precision, rounding)
        : null
    );

  private static CoverageCountViewModel ToCount(int covered, int total, decimal? ratio, int precision, RoundingMode rounding) =>
    new(covered, total, ratio, DisplayFormatter.Percent(ratio, precision, rounding));

  private static int Saturate(long sum) => (int)Math.Min(sum, int.MaxValue);

  #endregion
}
