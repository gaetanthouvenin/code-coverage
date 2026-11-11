// ------------------------------------------------------------------------------------------------
// <copyright file="DiffTableRenderer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Application.Services.Comments;

/// <summary>Writes the <c>diff</c> section of the comment: the totals of the base and of the head, side by side.</summary>
/// <remarks>
///   <para>
///     The table is the one of <c>diff_to_string</c> of Codecov, in a <c>diff</c> code block so that the forge
///     colours its lines: a line starts with <c>+</c> when the change is good and with <c>-</c> when it is bad,
///     whatever the sign of the number. More hits is good; more misses or more partials is bad. Files, lines and
///     branches are neither.
///   </para>
///   <para>
///     A row whose two values are zero is left out, as Codecov does. The <c>Complexity</c> row of Codecov is never
///     written: the model holds no complexity.
///   </para>
/// </remarks>
internal static class DiffTableRenderer
{
  #region Fields

  /// <summary>The number of spaces between two columns.</summary>
  private const int Padding = 3;

  /// <summary>The title of the table.</summary>
  private const string Title = "Coverage Diff";

  /// <summary>The cell that opens and closes the row of the titles.</summary>
  private const string Edge = "##";

  /// <summary>The prefix of a row that is neither good nor bad.</summary>
  private const string Neutral = " ";

  #endregion

  #region Methods

  /// <summary>Writes the table.</summary>
  /// <param name="baseTitle">The title of the column of the base: its branch.</param>
  /// <param name="headTitle">The title of the column of the head: the pull request.</param>
  /// <param name="baseTotals">The totals of the base, or <see langword="null" /> when it has no report.</param>
  /// <param name="headTotals">The totals of the head.</param>
  /// <param name="baseFiles">The number of files of the report of the base, or <see langword="null" /> without one.</param>
  /// <param name="headFiles">The number of files of the report of the head.</param>
  /// <param name="config">The configuration, for the precision and the rounding.</param>
  /// <returns>The lines of the section, the fences of the code block included.</returns>
  /// <exception cref="ArgumentNullException">A title or <paramref name="config" /> is <see langword="null" />.</exception>
  public static IReadOnlyList<string> Render(
    string baseTitle,
    string headTitle,
    CoverageTotals? baseTotals,
    CoverageTotals headTotals,
    int? baseFiles,
    int headFiles,
    CoverageConfig config)
  {
    ArgumentNullException.ThrowIfNull(baseTitle);
    ArgumentNullException.ThrowIfNull(headTitle);
    ArgumentNullException.ThrowIfNull(config);

    string[] titles = [Edge, baseTitle, headTitle, "+/-", Edge];
    string[]?[][] groups =
    [
      [CoverageRow(baseTotals?.Coverage, headTotals.Coverage, baseTotals is not null, config)],
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
    var spacer = new string('=', width);
    var lines = new List<string> { "```diff", $"@@{Center(Title, width - 4)}@@", Line(titles, widths) };

    foreach (var group in groups)
    {
      string[][] written = [.. group.OfType<string[]>()];

      if (written.Length == 0)
      {
        continue;
      }

      lines.Add(spacer);
      lines.AddRange(written.Select(row => Line(row, widths)));
    }

    lines.Add("```");

    return lines;
  }

  /// <summary>Builds the row of the coverage.</summary>
  /// <param name="baseCoverage">The coverage of the base, or <see langword="null" />.</param>
  /// <param name="headCoverage">The coverage of the head, or <see langword="null" /> without any coverable line.</param>
  /// <param name="hasBase">Whether the base has a report.</param>
  /// <param name="config">The configuration.</param>
  /// <returns>The cells of the row.</returns>
  private static string[] CoverageRow(decimal? baseCoverage, decimal? headCoverage, bool hasBase, CoverageConfig config)
  {
    var change = hasBase && baseCoverage is not null && headCoverage is not null ? headCoverage - baseCoverage : null;

    var text = change is null or 0 ? string.Empty : CommentFormatting.Delta(change, config);
    var sign = change switch
    {
      null or 0 => Neutral,
      > 0 => "+",
      var _ => "-",
    };

    return [$"{sign} Coverage", CommentFormatting.Percent(baseCoverage, config), CommentFormatting.Percent(headCoverage, config), text, string.Empty];
  }

  /// <summary>Builds the row of a count.</summary>
  /// <param name="title">The title of the row.</param>
  /// <param name="baseValue">The count of the base, or <see langword="null" /> without a report.</param>
  /// <param name="headValue">The count of the head.</param>
  /// <param name="rise">The prefix of the row when the count rises.</param>
  /// <param name="fall">The prefix of the row when the count falls.</param>
  /// <returns>The cells of the row, or <see langword="null" /> when both counts are zero.</returns>
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
      > 0 => "+" + CommentFormatting.Number(change.Value),
      var _ => CommentFormatting.Number(change.Value),
    };

    return
    [
      $"{sign} {title}",
      baseValue is { } value ? CommentFormatting.Number(value) : CommentFormatting.Missing,
      CommentFormatting.Number(headValue),
      text,
      string.Empty,
    ];
  }

  /// <summary>Writes one row: its first cell on the left, the others on the right.</summary>
  /// <param name="cells">The cells of the row.</param>
  /// <param name="widths">The width of each column.</param>
  /// <returns>The line, without the spaces that would end it.</returns>
  private static string Line(string[] cells, int[] widths) =>
    string.Join(new string(' ', Padding), cells.Select((cell, column) => column == 0 ? cell.PadRight(widths[column]) : cell.PadLeft(widths[column])))
          .TrimEnd();

  /// <summary>Centres a text; a space that cannot be shared goes to the right, as Python centres.</summary>
  /// <param name="text">The text.</param>
  /// <param name="width">The width to fill.</param>
  /// <returns>The text between its spaces.</returns>
  private static string Center(string text, int width)
  {
    var spaces = Math.Max(0, width - text.Length);

    return new string(' ', spaces / 2) + text + new string(' ', spaces - spaces / 2);
  }

  #endregion
}
