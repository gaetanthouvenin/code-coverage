// ------------------------------------------------------------------------------------------------
// <copyright file="RemovedCodeAdjuster.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Domain.Notifications;

/// <summary>
///   Gives a failing <c>project</c> status a second chance when the drop comes from removed code
///   (<c>removed_code_behavior</c>), as Codecov does. Pure.
/// </summary>
/// <remarks>
///   Removing covered lines lowers a coverage without any line being less tested. Each behaviour passes
///   the status on its own condition and adds its own words to the description.
/// </remarks>
public static class RemovedCodeAdjuster
{
  #region Fields

  /// <summary>What <c>removals_only</c> adds to the description.</summary>
  public const string RemovalsOnlySuffix = ", passed because this change only removed code";

  /// <summary>What <c>fully_covered_patch</c> adds when the patch has no line to cover.</summary>
  public const string PatchNotAffectedSuffix = ", passed because coverage was not affected by patch";

  /// <summary>What <c>fully_covered_patch</c> adds when every line of the patch is hit.</summary>
  public const string PatchFullyCoveredSuffix = ", passed because patch was fully covered by tests, and no indirect coverage changes";

  // Codecov passes the status when the head is less than 0.005 point under the adjusted base.
  private const decimal Tolerance = 0.005m;

  private const int StoredDecimals = 5;

  #endregion

  #region Methods

  /// <summary>Tells whether a failing project status passes after all, and with which words.</summary>
  /// <param name="rule">The project rule.</param>
  /// <param name="subject">What the rule is evaluated on, with a head and a base that both have coverage.</param>
  /// <param name="format">How the numbers are written.</param>
  /// <returns>What to add to the description of a status that passes, or <see langword="null" /> when it stays failing.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static string? Adjust(StatusRule rule, StatusSubject subject, StatusFormat format)
  {
    ArgumentNullException.ThrowIfNull(rule);
    ArgumentNullException.ThrowIfNull(subject);
    ArgumentNullException.ThrowIfNull(format);

    if (subject.Head is not { } head || subject.Base is not { } @base)
    {
      return null;
    }

    return rule.RemovedCode switch
    {
      RemovedCodeBehavior.AdjustBase => AdjustBase(rule, head, @base, subject.ComparisonDiff, format),
      RemovedCodeBehavior.RemovalsOnly => RemovalsOnly(head, @base, subject),
      RemovedCodeBehavior.FullyCoveredPatch => FullyCoveredPatch(subject),
      var _ => null,
    };
  }

  /// <summary>Computes the totals of the base lines that the diff removes.</summary>
  /// <param name="base">The view of the base report.</param>
  /// <param name="diff">The comparison diff.</param>
  /// <returns>The totals of the removed lines the base report covers.</returns>
  /// <remarks>A renamed file is read in the base under its previous path; a file without a patch removes nothing that is known.</remarks>
  internal static CoverageTotals Removed(ReportView @base, DiffSet diff)
  {
    int hits = 0, misses = 0, partials = 0;

    foreach (var file in diff.Files.Where(static f => f.HasPatch))
    {
      var totals = @base.TotalsOf(file.PreviousPath ?? file.Path, file.RemovedLines);

      hits += totals.Hits;
      misses += totals.Misses;
      partials += totals.Partials;
    }

    return new CoverageTotals(hits, misses, partials, 0, 0);
  }

  /// <summary>Applies <c>adjust_base</c>: the head is compared with the base without the lines the diff removed.</summary>
  /// <param name="rule">The rule.</param>
  /// <param name="head">The view of the head.</param>
  /// <param name="base">The view of the base.</param>
  /// <param name="diff">The comparison diff.</param>
  /// <param name="format">How the numbers are written.</param>
  /// <returns>The words of a status that passes, or <see langword="null" />.</returns>
  /// <remarks>Only against the base: a numeric target does not move with the code that is removed.</remarks>
  private static string? AdjustBase(StatusRule rule, ReportView head, ReportView @base, DiffSet diff, StatusFormat format)
  {
    if (!rule.Target.IsAuto || head.Totals.Coverage is not { } headCoverage)
    {
      return null;
    }

    var removed = Removed(@base, diff);
    var adjustedHits = @base.Totals.Hits - removed.Hits;
    var adjustedLines = @base.Totals.Lines - removed.Lines;

    if (adjustedLines <= 0)
    {
      return null;
    }

    var adjusted = 100m * adjustedHits / adjustedLines - rule.Threshold;

    if (Math.Round(adjusted, StoredDecimals, MidpointRounding.ToEven) - headCoverage >= Tolerance)
    {
      return null;
    }

    // Codecov writes max(0, rounded): a difference that is not positive reads "0", without decimals.
    var difference = StatusNumber.Round(headCoverage - adjusted, format);
    var increase = difference > 0 ? StatusNumber.Text(headCoverage - adjusted, format) : "0";

    return $", passed because coverage increased by {increase}% when compared to adjusted base ({StatusNumber.Text(adjusted, format)}%)";
  }

  /// <summary>Applies <c>removals_only</c>: the change removes covered lines, adds none and changes nothing elsewhere.</summary>
  /// <param name="head">The view of the head.</param>
  /// <param name="base">The view of the base.</param>
  /// <param name="subject">What the rule is evaluated on.</param>
  /// <returns>The words of a status that passes, or <see langword="null" />.</returns>
  private static string? RemovalsOnly(ReportView head, ReportView @base, StatusSubject subject)
  {
    if (subject.IndirectChanges.Count > 0)
    {
      return null;
    }

    var added = subject.ComparisonDiff.Files.Where(static f => f.HasPatch).Sum(f => head.TotalsOf(f.Path, f.AddedLines).Lines);

    return added == 0 && Removed(@base, subject.ComparisonDiff).Lines > 0 ? RemovalsOnlySuffix : null;
  }

  /// <summary>Applies <c>fully_covered_patch</c>: nothing changed elsewhere, and the patch is fully hit or has nothing to cover.</summary>
  /// <param name="subject">What the rule is evaluated on.</param>
  /// <returns>The words of a status that passes, or <see langword="null" />.</returns>
  private static string? FullyCoveredPatch(StatusSubject subject)
  {
    if (subject.IndirectChanges.Count > 0)
    {
      return null;
    }

    if (subject.Patch.Totals.Lines == 0)
    {
      return PatchNotAffectedSuffix;
    }

    return subject.Patch.Totals.Hits == subject.Patch.Totals.Lines ? PatchFullyCoveredSuffix : null;
  }

  #endregion
}