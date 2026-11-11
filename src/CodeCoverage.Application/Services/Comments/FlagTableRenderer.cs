// ------------------------------------------------------------------------------------------------
// <copyright file="FlagTableRenderer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Application.Services.Comments;

/// <summary>Writes the <c>flags</c> section of the comment: one row per flag, compared with its base.</summary>
/// <remarks>
///   The flags are case-sensitive and written in ordinal order. A flag whose every session was carried forward is
///   left out unless <c>comment.show_carryforward_flags</c> asks for it, and then says which commit it comes from.
///   A flag the base had and the head no longer has is written with a question mark. Without any row, the section
///   is not written at all.
/// </remarks>
internal static class FlagTableRenderer
{
  #region Methods

  /// <summary>Writes the table.</summary>
  /// <param name="content">What the comment is written from.</param>
  /// <returns>The lines of the section; empty when no flag is shown.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="content" /> is <see langword="null" />.</exception>
  public static IReadOnlyList<string> Render(CommentContent content)
  {
    ArgumentNullException.ThrowIfNull(content);

    var showCarriedForward = content.Config.Comment.ShowCarryforwardFlags;
    FlagComparison[] flags =
    [
      .. content.Context.Comparison.Result.Flags
                .Where(flag => showCarriedForward || !flag.CarriedForward)
                .OrderBy(static flag => flag.Flag, StringComparer.Ordinal),
    ];

    if (flags.Length == 0)
    {
      return [];
    }

    return ["| Flag | Coverage \u0394 | |", "|---|---|---|", .. flags.Select(flag => WriteRow(flag, content))];
  }

  /// <summary>Writes the row of one flag.</summary>
  /// <param name="flag">The flag.</param>
  /// <param name="content">What the comment is written from.</param>
  /// <returns>The row.</returns>
  private static string WriteRow(FlagComparison flag, CommentContent content)
  {
    var name = CommentFormatting.Escape(flag.Flag);

    if (flag.Head is not { } head)
    {
      return $"| {name} | `{CommentFormatting.Missing}` | |";
    }

    var metrics = CommentFormatting.Metrics(head, flag.Base, flag.Patch.Coverage, content.Config, MissingBase.Unknown);
    var note = flag.CarriedForward ? WriteOrigin(flag.Flag, content) : CommentFormatting.Arrow(flag.Delta);

    return note.Length == 0 ? $"| {name} | {metrics} | |" : $"| {name} | {metrics} | {note} |";
  }

  /// <summary>Tells where the coverage of a carried forward flag comes from.</summary>
  /// <param name="flag">The flag.</param>
  /// <param name="content">What the comment is written from.</param>
  /// <returns>The note, with the commit the sessions of the flag were carried forward from when it is known.</returns>
  private static string WriteOrigin(string flag, CommentContent content)
  {
    var origin = content.Context.HeadSessions
                        .Select(static session => session.Session)
                        .Where(session => session.Kind is SessionKind.CarriedForward && session.Flags.Contains(flag, StringComparer.Ordinal))
                        .OrderBy(static session => session.Number)
                        .Select(static session => session.CarriedForwardFromSha)
                        .OfType<GitSha>()
                        .FirstOrDefault();

    return origin is null
             ? "Carriedforward"
             : $"Carriedforward from {CommentFormatting.Link($"`{CommentFormatting.ShortSha(origin)}`", content.Links.Commit(origin))}";
  }

  #endregion
}
