// ------------------------------------------------------------------------------------------------
// <copyright file="PartialsAsHitsTransform.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Coverage;

/// <summary>Applies the <c>parsers.{format}.partials_as_hits</c> option to a parsed report.</summary>
/// <remarks>
///   <para>
///     Codecov reads the option in three of these parsers only, each with its own rule, and only those
///     are reproduced. OpenCover and Clover have no such option: their reports are never changed.
///   </para>
///   <list type="bullet">
///     <item>
///       Cobertura (<c>cobertura.py</c>): a branch line with at least one covered branch, even a
///       fully covered one, becomes a plain line with one hit.
///     </item>
///     <item>
///       LCOV and JaCoCo (<c>lcov.py</c>, <c>jacoco.py</c>): only a partial line changes. It counts
///       as a hit and keeps its kind, branch or method. Having all its branches covered stands for
///       the value <c>1</c> of Codecov: when the sessions are merged, a hit wins in both.
///     </item>
///   </list>
///   <para>
///     Codecov converts each line as it reads it, before the repeated lines of a report are merged;
///     here the lines are already merged. A line repeated in a same report with different branch
///     data can therefore end differently: two values <c>0/n</c> whose missing branches differ are a
///     partial in Codecov and a hit here, and in Cobertura a <c>1/2</c> that names no branch, merged
///     with a <c>0/2</c> that names them, is a hit in Codecov and a miss here.
///   </para>
/// </remarks>
public static class PartialsAsHitsTransform
{
  #region Methods

  /// <summary>Converts the partial lines of a report, if its format asks for it.</summary>
  /// <param name="report">The parsed report.</param>
  /// <param name="config">The effective configuration.</param>
  /// <returns>The converted report, or <paramref name="report" /> when the option is off.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="report" /> or <paramref name="config" /> is <see langword="null" />.
  /// </exception>
  public static ParsedReport Apply(ParsedReport report, CoverageConfig config)
  {
    ArgumentNullException.ThrowIfNull(report);
    ArgumentNullException.ThrowIfNull(config);

    Func<LineCoverage, LineCoverage>? convert = report.Format switch
    {
      ReportFormat.Cobertura when config.Cobertura.PartialsAsHits => ToPlainHit,
      ReportFormat.Lcov when config.Lcov.PartialsAsHits => ToBranchHit,
      ReportFormat.Jacoco when config.Jacoco.PartialsAsHits => ToBranchHit,
      var _ => null,
    };

    if (convert is null)
    {
      return report;
    }

    return report with
    {
      Files = [.. report.Files.Select(file => file with { Lines = [.. file.Lines.Select(convert)] })],
    };
  }

  /// <summary>Applies the rule of Cobertura.</summary>
  /// <param name="line">The line.</param>
  /// <returns>A plain line with one hit if a branch is covered; otherwise, the line.</returns>
  private static LineCoverage ToPlainHit(LineCoverage line) =>
    line.Covered > 0 ? line with { Hits = 1, Covered = 0, Total = 0, MissingBranches = null } : line;

  /// <summary>Applies the rule of LCOV and JaCoCo.</summary>
  /// <param name="line">The line.</param>
  /// <returns>The line, all covered if it was partial; otherwise, the line.</returns>
  private static LineCoverage ToBranchHit(LineCoverage line) =>
    CoverageMath.Classify(line) is LineState.Partial
      ? line with { Hits = 1, Covered = line.Total, MissingBranches = null }
      : line;

  #endregion
}
