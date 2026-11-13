// ------------------------------------------------------------------------------------------------
// <copyright file="DiffTableLayout.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Mapping;

/// <summary>
///   Lays out the <c>diff</c> table of a comparison with the rows, signs and invariant numbers of the comment of the pull
///   request (<c>DiffTableRenderer</c> of the Application, internal there): Coverage, Files, Lines, Branches, Hits,
///   Misses and Partials, a row whose two values are zero left out.
/// </summary>
internal static class DiffTableLayout
{
  #region Fields

  private const int Padding = 3;

  private const string Title = "Coverage Diff";

  private const string Edge = "##";

  private const string Neutral = " ";

  private const string Missing = "?";

  #endregion

  #region Methods

  /// <summary>Lays the table out.</summary>
  /// <param name="baseTitle">The title of the column of the base: its branch.</param>
  /// <param name="headTitle">The title of the column of the head.</param>
  /// <param name="baseTotals">The totals of the base, or <see langword="null" /> without a base report.</param>
  /// <param name="headTotals">The totals of the head.</param>
  /// <param name="baseFiles">The number of files of the base, or <see langword="null" /> without a base report.</param>
  /// <param name="headFiles">The number of files of the head.</param>
  /// <param name="precision">The number of decimals of a coverage.</param>
  /// <param name="rounding">How a coverage is rounded.</param>
  /// <returns>The lines, without the fences of a code block.</returns>
  public static IReadOnlyList<DiffLineViewModel> Lay(
    string baseTitle,
    string headTitle,
    CoverageTotals? baseTotals,
    CoverageTotals headTotals,
    int? baseFiles,
    int headFiles,
    int precision,
    RoundingMode rounding)
  {
    string[] titles = [Edge, baseTitle, headTitle, "+/-", Edge];
    string[]?[][] groups =
    [
      [CoverageRow(baseTotals?.Coverage, headTotals.Coverage, baseTotals is not null, precision, rounding)],
      [
        CountRow("Files", baseFiles, headFiles, Neutral, Neutral),
        CountRow("Lines", baseTotals?.Lines, headTotals.Lines, Neutral, Neutral),
        CountRow("Branches", baseTotals?.Branches, headTotals.Branches, Neutral, Neutral),
      ],
      [
        CountRow("Hits", baseTotals?.Hits, headTotals.Hits, "+", "-"),
        CountRow("Misses", baseTotals?.Misses, headTotals.Misses, "-", "+"),
        CountRow("Partials", baseTotals?.Partials, headTotals.Partials, "-", "+"),
      ],
    ];

    string[][] rows = [.. groups.SelectMany(static group => group).OfType<string[]>()];
    int[] widths = [.. Enumerable.Range(0, titles.Length).Select(column => rows.Append(titles).Max(row => row[column].Length))];
    var width = widths.Sum() + Padding * (widths.Length - 1);
    var spacer = new DiffLineViewModel(new string('=', width), DiffLineKind.Plain);
    var lines = new List<DiffLineViewModel>
    {
      new($"@@{Center(Title, width - 4)}@@", DiffLineKind.Head),
      new(Line(titles, widths), DiffLineKind.Head),
    };

    foreach (var group in groups)
    {
      string[][] written = [.. group.OfType<string[]>()];

      if (written.Length == 0)
      {
        continue;
      }

      lines.Add(spacer);
      lines.AddRange(written.Select(row => new DiffLineViewModel(Line(row, widths), KindOf(row[0]))));
    }

    return lines;
  }

  private static DiffLineKind KindOf(string first) => first[0] switch
  {
    '+' => DiffLineKind.Add,
    '-' => DiffLineKind.Delete,
    var _ => DiffLineKind.Plain,
  };

  private static string Percent(decimal? value, int precision, RoundingMode rounding) =>
    value is null ? Missing : CoverageMath.Format(value, precision, rounding) + "%";

  private static string[] CoverageRow(decimal? baseCoverage, decimal? headCoverage, bool hasBase, int precision, RoundingMode rounding)
  {
    var change = hasBase && baseCoverage is not null && headCoverage is not null ? headCoverage - baseCoverage : null;
    var text = change is null or 0 ? string.Empty : CoverageMath.FormatDelta(change, precision, rounding) + "%";
    var sign = change switch
    {
      null or 0 => Neutral,
      > 0 => "+",
      var _ => "-",
    };

    return [$"{sign} Coverage", Percent(baseCoverage, precision, rounding), Percent(headCoverage, precision, rounding), text, string.Empty];
  }

  private static string[]? CountRow(string title, int? baseValue, int headValue, string rise, string fall)
  {
    if (baseValue is 0 && headValue == 0)
    {
      return null;
    }

    var change = headValue - baseValue;
    var sign = change switch
    {
      null or 0 => Neutral,
      > 0 => rise,
      var _ => fall,
    };
    var text = change switch
    {
      null or 0 => string.Empty,
      > 0 => "+" + change.Value.ToString(CultureInfo.InvariantCulture),
      var _ => change.Value.ToString(CultureInfo.InvariantCulture),
    };

    return
    [
      $"{sign} {title}",
      baseValue is { } value ? value.ToString(CultureInfo.InvariantCulture) : Missing,
      headValue.ToString(CultureInfo.InvariantCulture),
      text,
      string.Empty,
    ];
  }

  private static string Line(string[] cells, int[] widths) =>
    string.Join(new string(' ', Padding), cells.Select((cell, column) => column == 0 ? cell.PadRight(widths[column]) : cell.PadLeft(widths[column])))
          .TrimEnd();

  private static string Center(string text, int width)
  {
    var spaces = Math.Max(0, width - text.Length);

    return new string(' ', spaces / 2) + text + new string(' ', spaces - spaces / 2);
  }

  #endregion
}
