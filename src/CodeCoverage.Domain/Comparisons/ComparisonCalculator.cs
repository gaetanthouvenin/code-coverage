// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonCalculator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>Computes what a comparison shows: patch coverage, impacted files, flags and deltas. Pure.</summary>
public static class ComparisonCalculator
{
  #region Methods

  /// <summary>Computes the coverage of the lines a diff adds, read in the head report.</summary>
  /// <param name="head">The view of the head report.</param>
  /// <param name="diff">The patch diff.</param>
  /// <returns>The patch coverage.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <remarks>
  ///   Only the files the diff adds, modifies, renames or copies count, and in them only the added lines that
  ///   the head report holds: a line that cannot be covered is not part of a patch. A file without a patch
  ///   is listed as unavailable and counts for nothing.
  /// </remarks>
  public static PatchCoverage Patch(ReportView head, DiffSet diff)
  {
    ArgumentNullException.ThrowIfNull(head);
    ArgumentNullException.ThrowIfNull(diff);

    var files = new List<PatchFileCoverage>();
    int hits = 0, misses = 0, partials = 0, branches = 0, methods = 0;

    foreach (var file in diff.Files.Where(IsPatched).OrderBy(static f => f.Path, StringComparer.Ordinal))
    {
      if (!file.HasPatch)
      {
        files.Add(new PatchFileCoverage(file.Path, default, DiffUnavailable: true));

        continue;
      }

      var totals = head.TotalsOf(file.Path, file.AddedLines);

      if (totals.Lines == 0)
      {
        continue;
      }

      files.Add(new PatchFileCoverage(file.Path, totals, DiffUnavailable: false));
      hits += totals.Hits;
      misses += totals.Misses;
      partials += totals.Partials;
      branches += totals.Branches;
      methods += totals.Methods;
    }

    return new PatchCoverage(new CoverageTotals(hits, misses, partials, branches, methods), files);
  }

  /// <summary>Computes the change of coverage between two totals.</summary>
  /// <param name="head">The totals of the head, or <see langword="null" />.</param>
  /// <param name="base">The totals of the base, or <see langword="null" />.</param>
  /// <returns>
  ///   The coverage of the head minus the one of the base, or <see langword="null" /> when a side is missing
  ///   or has no line to cover.
  /// </returns>
  /// <remarks>
  ///   The difference of the two coverages as Codecov stores them, with five decimals, and nothing more: it
  ///   is rounded only when it is displayed.
  /// </remarks>
  public static decimal? Delta(CoverageTotals? head, CoverageTotals? @base) =>
    head?.Coverage is { } headCoverage && @base?.Coverage is { } baseCoverage ? headCoverage - baseCoverage : null;

  /// <summary>Lists the files of a diff that one report at least covers.</summary>
  /// <param name="base">The view of the base report, or <see langword="null" /> without a base.</param>
  /// <param name="head">The view of the head report.</param>
  /// <param name="diff">The diff.</param>
  /// <returns>The files, by path.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="head" /> or <paramref name="diff" /> is <see langword="null" />.</exception>
  /// <remarks>A renamed file is read in the base under its previous path.</remarks>
  public static IReadOnlyList<ImpactedFile> Files(ReportView? @base, ReportView head, DiffSet diff)
  {
    ArgumentNullException.ThrowIfNull(head);
    ArgumentNullException.ThrowIfNull(diff);

    var headTotals = head.Report.FileTotals.ToDictionary(static f => f.Path, static f => f.Totals, StringComparer.Ordinal);
    var baseTotals = @base?.Report.FileTotals.ToDictionary(static f => f.Path, static f => f.Totals, StringComparer.Ordinal);
    var files = new List<ImpactedFile>();

    foreach (var file in diff.Files.OrderBy(static f => f.Path, StringComparer.Ordinal))
    {
      CoverageTotals? inHead = headTotals.TryGetValue(file.Path, out var found) ? found : null;
      CoverageTotals? inBase = baseTotals is not null && baseTotals.TryGetValue(file.PreviousPath ?? file.Path, out var previous) ? previous : null;

      if (inHead is null && inBase is null)
      {
        continue;
      }

      var patch = IsPatched(file) && file.HasPatch ? head.TotalsOf(file.Path, file.AddedLines) : default;

      files.Add(new ImpactedFile(file.Path, file.PreviousPath, file.Status, inBase, inHead, patch));
    }

    return files;
  }

  /// <summary>Compares each flag of the head or of the base.</summary>
  /// <param name="baseSessions">The sessions of the base report, or <see langword="null" /> without a base.</param>
  /// <param name="headSessions">The sessions of the head report.</param>
  /// <param name="carriedForwardFlags">The flags of the head whose every session was carried forward.</param>
  /// <param name="diff">The patch diff.</param>
  /// <param name="pathsOf">Gives the paths a flag keeps: each flag is measured on its own paths, as its totals are.</param>
  /// <returns>One comparison per flag, by flag in ordinal order.</returns>
  /// <exception cref="ArgumentNullException">An argument other than <paramref name="baseSessions" /> is <see langword="null" />.</exception>
  public static IReadOnlyList<FlagComparison> Flags(
    IReadOnlyList<SessionReport>? baseSessions,
    IReadOnlyList<SessionReport> headSessions,
    IReadOnlyCollection<string> carriedForwardFlags,
    DiffSet diff,
    Func<string, PathPatternSet> pathsOf)
  {
    ArgumentNullException.ThrowIfNull(headSessions);
    ArgumentNullException.ThrowIfNull(carriedForwardFlags);
    ArgumentNullException.ThrowIfNull(diff);
    ArgumentNullException.ThrowIfNull(pathsOf);

    var headFlags = headSessions.SelectMany(static s => s.Flags).ToHashSet(StringComparer.Ordinal);
    var baseFlags = (baseSessions ?? []).SelectMany(static s => s.Flags).ToHashSet(StringComparer.Ordinal);
    var comparisons = new List<FlagComparison>();

    foreach (var flag in headFlags.Union(baseFlags, StringComparer.Ordinal).Order(StringComparer.Ordinal))
    {
      string[] selection = [flag];
      var paths = pathsOf(flag);
      var head = headFlags.Contains(flag) ? ReportView.Build(headSessions, selection, paths) : null;
      var @base = baseFlags.Contains(flag) ? ReportView.Build(baseSessions!, selection, paths) : null;

      comparisons.Add(
        new FlagComparison(
          flag,
          @base?.Totals,
          head?.Totals,
          head is null ? PatchCoverage.Empty : Patch(head, diff),
          carriedForwardFlags.Contains(flag, StringComparer.Ordinal)
        )
      );
    }

    return comparisons;
  }

  /// <summary>Finds the flags whose every session was carried forward from an ancestor.</summary>
  /// <param name="sessions">The sessions of a commit.</param>
  /// <returns>The flags, case-sensitive.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sessions" /> is <see langword="null" />.</exception>
  /// <remarks>One uploaded session is enough for a flag to be its own: the flag is then measured on this commit.</remarks>
  public static IReadOnlySet<string> CarriedForwardFlags(IEnumerable<Session> sessions)
  {
    ArgumentNullException.ThrowIfNull(sessions);

    var carried = new HashSet<string>(StringComparer.Ordinal);
    var uploaded = new HashSet<string>(StringComparer.Ordinal);

    foreach (var session in sessions)
    {
      (session.Kind is SessionKind.CarriedForward ? carried : uploaded).UnionWith(session.Flags);
    }

    carried.ExceptWith(uploaded);

    return carried;
  }

  /// <summary>Tells whether a diff can add lines to a file.</summary>
  /// <param name="file">The file of the diff.</param>
  /// <returns><see langword="true" /> for a file added, modified, renamed or copied.</returns>
  private static bool IsPatched(FileDiff file) =>
    file.Status is FileDiffStatus.Added or FileDiffStatus.Modified or FileDiffStatus.Renamed or FileDiffStatus.Copied;

  #endregion
}