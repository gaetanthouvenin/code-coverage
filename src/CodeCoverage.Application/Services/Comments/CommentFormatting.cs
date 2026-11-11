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
