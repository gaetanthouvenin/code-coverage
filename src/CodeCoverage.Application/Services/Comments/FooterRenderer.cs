// ------------------------------------------------------------------------------------------------
// <copyright file="FooterRenderer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Application.Services.Comments;

/// <summary>Writes the <c>footer</c> section of the comment: the legend, the commits compared and the uploads.</summary>
internal static class FooterRenderer
{
  #region Methods

  /// <summary>Writes the footer.</summary>
  /// <param name="content">What the comment is written from.</param>
  /// <returns>The line of the footer.</returns>
  /// <remarks>
  ///   The uploads counted are the sessions of the head, those carried forward from an ancestor included, as the
  ///   report holds them; the uploads that failed are counted apart.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="content" /> is <see langword="null" />.</exception>
  public static string Render(CommentContent content)
  {
    ArgumentNullException.ThrowIfNull(content);

    var comparison = content.Context.Comparison;
    var head = CommentFormatting.ShortSha(content.Head.Sha);
    var compared = comparison.Result.Base is null ? null : comparison.ComparedToSha;
    var sessions = content.Context.HeadSessions;
    var carried = sessions.Count(static session => session.Session.Kind is SessionKind.CarriedForward);
    var parts = new List<string>
    {
      // Inside an HTML element, the angle brackets of the legend would be read as a tag.
      "\u0394 = absolute &lt;relative&gt; (impact), \u00f8 = not affected, ? = missing data",
      compared is null ? $"Last update {head}" : $"Last update {CommentFormatting.ShortSha(compared)}...{head}",
      carried == 0
        ? CommentFormatting.Count(sessions.Count, "upload")
        : $"{CommentFormatting.Count(sessions.Count, "upload")} ({CommentFormatting.Number(carried)} carried forward)",
    };

    if (content.FailedUploadCount > 0)
    {
      parts.Add($"{CommentFormatting.Count(content.FailedUploadCount, "upload")} failed");
    }

    return $"<sub>{string.Join(" \u00b7 ", parts)}</sub>";
  }

  #endregion
}
