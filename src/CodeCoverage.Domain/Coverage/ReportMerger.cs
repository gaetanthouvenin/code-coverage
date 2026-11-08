// ------------------------------------------------------------------------------------------------
// <copyright file="ReportMerger.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Coverage;

/// <summary>Merges coverage like Codecov, whatever the order of the sessions.</summary>
public static class ReportMerger
{
  #region Methods

  /// <summary>Merges two values of one line.</summary>
  /// <param name="a">The first value.</param>
  /// <param name="b">The second value.</param>
  /// <returns>The merged line.</returns>
  /// <remarks>
  ///   The rules are those of <c>merge_line_session</c> and <c>merge_coverage</c>, except where
  ///   they depend on the order of the sessions: the total of a merged branch line is always the
  ///   largest one, and the method flag always wins. The merge is commutative and associative.
  /// </remarks>
  /// <exception cref="ArgumentException">The two values are not of the same line.</exception>
  public static LineCoverage MergeLine(LineCoverage a, LineCoverage b)
  {
    ArgumentException.ThrowIf(a.Line != b.Line, "Only two values of the same line can be merged.", nameof(b));

    var merged = (a.IsBranch, b.IsBranch) switch
    {
      (true, true) => MergeBranches(a, b),
      (true, false) => MergeBranchWithPlainLine(a, b),
      (false, true) => MergeBranchWithPlainLine(b, a),
      var _ => new LineCoverage(a.Line, 0, 0, 0, false, null),
    };

    return merged with { Hits = Math.Max(a.Hits, b.Hits), IsMethod = a.IsMethod || b.IsMethod };
  }

  /// <summary>Merges reports, usually the sessions of a commit.</summary>
  /// <param name="reports">The reports, in any order.</param>
  /// <returns>The merged report; it has no file when there is no report.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="reports" /> is <see langword="null" />.
  /// </exception>
  public static CoverageReport Merge(IEnumerable<CoverageReport> reports)
  {
    ArgumentNullException.ThrowIfNull(reports);

    return CoverageReport.Create(reports.SelectMany(report => report.Files));
  }

  /// <summary>Merges a branch line with a line without branch data.</summary>
  /// <param name="branch">The branch line.</param>
  /// <param name="plain">The other line.</param>
  /// <returns>The branch line, all covered when the other line was executed.</returns>
  private static LineCoverage MergeBranchWithPlainLine(LineCoverage branch, LineCoverage plain)
  {
    if (plain.Hits <= 0)
    {
      return branch;
    }

    return branch with
    {
      Covered = branch.Total,
      MissingBranches = branch.MissingBranches is null ? null : [],
    };
  }

  /// <summary>Merges two branch lines.</summary>
  /// <param name="a">The first line.</param>
  /// <param name="b">The second line.</param>
  /// <returns>The merged line, whose total is the largest of both.</returns>
  private static LineCoverage MergeBranches(LineCoverage a, LineCoverage b)
  {
    var total = Math.Max(a.Total, b.Total);
    var missing = MergeMissingBranches(a, b);

    if (missing is not null)
    {
      return a with
      {
        Covered = Math.Max(total - missing.Length, 0),
        Total = total,
        MissingBranches = missing,
      };
    }

    var isFullyCovered = a.Covered == a.Total || b.Covered == b.Total;
    var covered = isFullyCovered ? total : Math.Max(a.Covered, b.Covered);

    return a with { Covered = covered, Total = total, MissingBranches = null };
  }

  /// <summary>Gets the branches that no line covers.</summary>
  /// <param name="a">The first line.</param>
  /// <param name="b">The second line.</param>
  /// <returns>
  ///   The identifiers, sorted and distinct; <see langword="null" /> when neither line names its
  ///   branches.
  /// </returns>
  private static string[]? MergeMissingBranches(LineCoverage a, LineCoverage b)
  {
    if (a.MissingBranches is null)
    {
      return b.MissingBranches is null ? null : KeepUnlessHit(b.MissingBranches, a);
    }

    if (b.MissingBranches is null)
    {
      return KeepUnlessHit(a.MissingBranches, b);
    }

    return Normalize(a.MissingBranches.Intersect(b.MissingBranches, StringComparer.Ordinal));
  }

  /// <summary>Gets the missing branches when only one line names them.</summary>
  /// <param name="missing">The identifiers named by one line.</param>
  /// <param name="other">The line that names none.</param>
  /// <returns>
  ///   Nothing when the other line is a hit, since no branch is left to cover; otherwise the
  ///   identifiers, the other line telling nothing about them.
  /// </returns>
  private static string[] KeepUnlessHit(string[] missing, LineCoverage other)
  {
    return CoverageMath.Classify(other) == LineState.Hit ? [] : Normalize(missing);
  }

  /// <summary>Puts identifiers in the single form that makes the merge deterministic.</summary>
  /// <param name="missing">The identifiers.</param>
  /// <returns>The identifiers, sorted and distinct.</returns>
  private static string[] Normalize(IEnumerable<string> missing)
  {
    return [.. missing.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
  }

  #endregion
}
