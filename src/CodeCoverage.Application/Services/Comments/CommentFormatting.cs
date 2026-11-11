// ------------------------------------------------------------------------------------------------
// <copyright file="CommentFormatting.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Application.Services.Comments;

/// <summary>The texts every section of the pull request comment writes the same way.</summary>
/// <remarks>
///   Every number goes through <see cref="CoverageMath" /> with <c>coverage.precision</c> and
///   <c>coverage.round</c>, and through the invariant culture: the body, and so its hash, is the same on every
///   machine.
/// </remarks>
internal static class CommentFormatting
{
  #region Fields

  /// <summary>The text of a value that is missing, as the legend of the comment names it.</summary>
  public const string Missing = "?";

  /// <summary>
  ///   What a cell of coverage writes for a coverage, or a change, that cannot be computed although the base is there:
  ///   the empty set, as <c>make_metrics</c> of Codecov writes it.
  /// </summary>
  public const string NotComputed = "∅";

  /// <summary>The number of characters of a SHA that the comment shows.</summary>
  private const int ShortShaLength = 7;

  #endregion

  #region Methods

  /// <summary>Formats a coverage, with its percent sign.</summary>
  /// <param name="value">The coverage, or <see langword="null" />.</param>
  /// <param name="config">The configuration, for the precision and the rounding.</param>
  /// <returns>The text, or <see cref="Missing" /> for <see langword="null" />.</returns>
  public static string Percent(decimal? value, CoverageConfig config) =>
    value is null ? Missing : CoverageMath.Format(value, config.Precision, config.Rounding) + "%";

  /// <summary>Formats a change of coverage, with its sign and its percent sign.</summary>
  /// <param name="value">The change, or <see langword="null" />.</param>
  /// <param name="config">The configuration, for the precision and the rounding.</param>
  /// <returns>
  ///   The text; <see cref="Missing" /> for <see langword="null" />, and <see cref="CoverageMath.NoImpact" />
  ///   for exactly zero.
  /// </returns>
  public static string Delta(decimal? value, CoverageConfig config) =>
    value switch
    {
      null => Missing,
      0 => CoverageMath.NoImpact,
      var _ => CoverageMath.FormatDelta(value, config.Precision, config.Rounding) + "%",
    };

  /// <summary>Writes the cell that compares a coverage with its base, as <c>make_metrics</c> of Codecov does.</summary>
  /// <param name="head">The totals of the head.</param>
  /// <param name="baseTotals">The totals of the base, or <see langword="null" /> when the base holds none.</param>
  /// <param name="patch">The coverage of the lines of the patch, or <see langword="null" /> when the patch has no coverable line.</param>
  /// <param name="config">The configuration, for the precision and the rounding.</param>
  /// <param name="missingBase">What the change says when the base does not hold what the cell describes.</param>
  /// <returns>
  ///   The cell: the coverage of the head, the one of the patch between angle brackets, and the change between
  ///   parentheses; <see cref="CoverageMath.NoImpact" /> for no patch line or no change. Without a base, the change is
  ///   what <paramref name="missingBase" /> says; with a base but a coverage that cannot be computed, it is
  ///   <see cref="NotComputed" />, as <c>make_metrics</c> of Codecov writes them.
  /// </returns>
  public static string Metrics(CoverageTotals head, CoverageTotals? baseTotals, decimal? patch, CoverageConfig config, MissingBase missingBase)
  {
    var absolute = head.Coverage is null ? NotComputed : Percent(head.Coverage, config);
    var relative = patch is null ? CoverageMath.NoImpact : Percent(patch, config);
    var impact = baseTotals is not { } before
                   ? missingBase is MissingBase.Unknown ? Missing : CoverageMath.NoImpact
                   : head.Coverage is { } after && before.Coverage is { } was
                     ? Delta(after - was, config)
                     : NotComputed;

    return $"`{absolute} <{relative}> ({impact})`";
  }

  /// <summary>Writes the arrow of a change of coverage.</summary>
  /// <param name="change">The change, not rounded, or <see langword="null" /> when it cannot be known.</param>
  /// <returns>An arrow up or down, or nothing for no change or an unknown one.</returns>
  public static string Arrow(decimal? change) =>
    change switch
    {
      > 0 => ":arrow_up:",
      < 0 => ":arrow_down:",
      var _ => string.Empty,
    };

  /// <summary>Formats a count.</summary>
  /// <param name="value">The count.</param>
  /// <returns>The digits, without any separator.</returns>
  public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

  /// <summary>Formats a count with the noun it counts.</summary>
  /// <param name="value">The count.</param>
  /// <param name="singular">The noun, in the singular; its plural takes an <c>s</c>.</param>
  /// <returns>The text, such as <c>1 line</c> or <c>3 lines</c>.</returns>
  public static string Count(int value, string singular) =>
    value == 1 ? $"1 {singular}" : string.Create(CultureInfo.InvariantCulture, $"{value} {singular}s");

  /// <summary>Gives the short form of a SHA.</summary>
  /// <param name="sha">The SHA.</param>
  /// <returns>Its first 7 characters.</returns>
  public static string ShortSha(GitSha sha) => sha.Value[..ShortShaLength];

  /// <summary>Writes a link, or its text alone when it leads nowhere.</summary>
  /// <param name="text">The text of the link, already written in Markdown.</param>
  /// <param name="url">The address, or <see langword="null" /> when the instance has no public address.</param>
  /// <returns>The Markdown of the link.</returns>
  public static string Link(string text, string? url) => url is null ? text : $"[{text}]({url})";

  /// <summary>Escapes a text that Markdown would otherwise read, in a sentence or in a cell of a table.</summary>
  /// <param name="text">The text, such as a path, a flag or a branch.</param>
  /// <returns>The text with the characters Markdown reads escaped; a line break becomes a space.</returns>
  public static string Escape(string text)
  {
    var escaped = new System.Text.StringBuilder(text.Length);

    foreach (var character in text)
    {
      switch (character)
      {
        case '\\' or '`' or '*' or '_' or '|' or '[' or ']' or '<' or '>' or '~':
          escaped.Append('\\').Append(character);

          break;
        case '\r' or '\n':
          escaped.Append(' ');

          break;
        default:
          escaped.Append(character);

          break;
      }
    }

    return escaped.ToString();
  }

  #endregion
}
