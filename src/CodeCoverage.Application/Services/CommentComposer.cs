// ------------------------------------------------------------------------------------------------
// <copyright file="CommentComposer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services.Comments;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Application.Services;

/// <summary>Writes the body of the comment of a pull request (ARCHITECTURE §8.4).</summary>
/// <remarks>
///   <para>
///     The body starts with the hidden marker, then the title and the header. The other sections follow in the
///     order of <c>comment.layout</c>, each as often as it is listed, folded under one <c>details</c> element;
///     the footer closes the body. The header comes first wherever the layout lists it, and is written even
///     when the layout lists none (<c>condensed_header</c> of Codecov, which writes the same lines).
///   </para>
///   <para>
///     Pure: the same content gives the same text, with line feeds only, so that its hash tells whether the
///     comment on the forge is up to date.
///   </para>
/// </remarks>
internal static class CommentComposer
{
  #region Fields

  /// <summary>The hidden first line of every comment this application writes.</summary>
  public const string Marker = "<!-- code-coverage:comment -->";

  /// <summary>The line that opens the folded part.</summary>
  private const string DetailsOpening = "<details><summary>Additional details and impacted files</summary>";

  /// <summary>The line that closes the folded part.</summary>
  private const string DetailsClosing = "</details>";

  #endregion

  #region Methods

  /// <summary>Writes the body of the comment.</summary>
  /// <param name="content">What the comment is written from.</param>
  /// <returns>The Markdown of the comment, without a final line feed.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="content" /> is <see langword="null" />.</exception>
  public static string Compose(CommentContent content)
  {
    ArgumentNullException.ThrowIfNull(content);

    var lines = new List<string>
    {
      content.Marker,
      $"## {CommentFormatting.Link(CommentFormatting.Escape(content.Title), content.Links.Pull(content.Pull.Number))} Report",
    };

    lines.AddRange(WriteHeader(content));
    lines.Add(string.Empty);

    IReadOnlyList<string>[] folded =
    [
      .. content.Config.Comment.Layout.Select(section => WriteSection(section, content)).Where(static section => section.Count > 0),
    ];

    if (folded.Length > 0)
    {
      if (content.WrapDetails)
      {
        lines.Add(DetailsOpening);
        lines.Add(string.Empty);
      }

      foreach (var section in folded)
      {
        lines.AddRange(section);
        lines.Add(string.Empty);
      }

      if (content.WrapDetails)
      {
        lines.Add(DetailsClosing);
        lines.Add(string.Empty);
      }
    }

    while (lines[^1].Length == 0)
    {
      lines.RemoveAt(lines.Count - 1);
    }

    return string.Join('\n', lines);
  }

  /// <summary>Writes one of the sections that are folded.</summary>
  /// <param name="section">The section, as the layout names it.</param>
  /// <param name="content">What the comment is written from.</param>
  /// <returns>The lines of the section; empty for a section that is written elsewhere, or that has nothing to show.</returns>
  private static IReadOnlyList<string> WriteSection(CommentSection section, CommentContent content)
  {
    var comparison = content.Context.Comparison.Result;

    return section switch
    {
      // The project coverage is hidden with everything that details it.
      CommentSection.Diff when !content.Config.Comment.HideProjectCoverage => DiffTableRenderer.Render(
        content.Pull.BaseBranch,
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"#{content.Pull.Number}"),
        comparison.Base,
        comparison.Head,
        comparison.Base is null ? null : content.BaseFileCount,
        content.HeadFileCount,
        content.Config
      ),
      var _ => [],
    };
  }

  /// <summary>Writes the header: the patch, the project, and what the reader must know about the base.</summary>
  /// <param name="content">What the comment is written from.</param>
  /// <returns>The lines of the header.</returns>
  private static List<string> WriteHeader(CommentContent content)
  {
    var comparison = content.Context.Comparison.Result;
    var lines = new List<string> { WritePatch(content) };

    if (!content.Config.Comment.HideProjectCoverage)
    {
      lines.AddRange(WriteProject(content));
    }

    if (comparison.DiffIncomplete)
    {
      lines.Add("> :warning: Diff incomplete (more than 3000 files); patch coverage is partial.");
    }

    if (content.ConfigInvalid)
    {
      lines.Add("> :warning: The configuration file of this commit is not valid and was ignored.");
    }

    return lines;
  }

  /// <summary>Writes the line of the patch coverage.</summary>
  /// <param name="content">What the comment is written from.</param>
  /// <returns>The line.</returns>
  private static string WritePatch(CommentContent content)
  {
    var patch = content.Context.Comparison.Result.Patch.Totals;
    var missing = patch.Misses + patch.Partials;

    return missing == 0
             ? ":white_check_mark: All modified and coverable lines are covered by tests."
             : $":x: Patch coverage is `{CommentFormatting.Percent(patch.Coverage, content.Config)}` with `{CommentFormatting.Count(missing, "line")}` in your changes missing coverage. Please review.";
  }

  /// <summary>Writes the line of the project coverage and the notes about the base it is compared to.</summary>
  /// <param name="content">What the comment is written from.</param>
  /// <returns>The lines.</returns>
  private static IEnumerable<string> WriteProject(CommentContent content)
  {
    var resolution = content.Context.Resolution;
    var comparison = content.Context.Comparison;
    var baseBranch = content.Pull.BaseBranch;

    // A base whose report is gone is no base, even when a commit was compared to.
    var comparedTo = comparison.Result.Base is null ? null : comparison.ComparedToSha;
    var project = $":white_check_mark: Project coverage is {CommentFormatting.Percent(comparison.Result.Head.Coverage, content.Config)}.";

    yield return comparedTo is null
                   ? project
                   : $"{project} Comparing base ({WriteCommit(comparedTo, content.Links)}) to head ({WriteCommit(content.Head.Sha, content.Links)}).";

    if (resolution.BehindBy is > 0 and var behind)
    {
      yield return $"> Report is {CommentFormatting.Count(behind, "commit")} behind head on {CommentFormatting.Escape(baseBranch)}.";
    }

    if (comparedTo is null)
    {
      var expected = resolution.ForkPoint ?? content.Pull.BaseShaGitHub;

      yield return $"> :warning: Please upload report for BASE (`{baseBranch}@{CommentFormatting.ShortSha(expected)}`).";
    }
    else if (resolution.Hops is > 0 and var hops)
    {
      yield return $"> Base report from `{CommentFormatting.ShortSha(comparedTo)}`, {CommentFormatting.Count(hops, "commit")} before the merge base.";
    }
  }

  /// <summary>Writes the short SHA of a commit, linked to its page.</summary>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="links">The addresses of the pages of the repository.</param>
  /// <returns>The Markdown of the link.</returns>
  private static string WriteCommit(GitSha sha, WebLinks links) =>
    CommentFormatting.Link($"`{CommentFormatting.ShortSha(sha)}`", links.Commit(sha));

  #endregion
}
