// ------------------------------------------------------------------------------------------------
// <copyright file="FileTableRenderer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Diffs;

namespace CodeCoverage.Application.Services.Comments;

/// <summary>Writes the <c>files</c> section of the comment: the files of the patch that miss coverage.</summary>
/// <remarks>
///   <para>
///     A file is listed when lines of the patch miss coverage in it, the most missing lines first, then the largest
///     change of coverage, then the path. A file the pull request removes, and that the base covered, is listed
///     after them, struck through and without numbers.
///   </para>
///   <para>
///     The rows over the limit are counted in a last row, and the files whose coverage changed although the patch
///     does not touch them in another one. Without anything to list, the section is not written at all.
///   </para>
/// </remarks>
internal static class FileTableRenderer
{
  #region Methods

  /// <summary>Writes the table.</summary>
  /// <param name="content">What the comment is written from.</param>
  /// <param name="limit">The number of files that get a row, at most.</param>
  /// <returns>The lines of the section; empty when there is nothing to list.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="content" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit" /> is negative.</exception>
  public static IReadOnlyList<string> Render(CommentContent content, int limit)
  {
    ArgumentNullException.ThrowIfNull(content);
    ArgumentOutOfRangeException.ThrowIfNegative(limit);

    var comparison = content.Context.Comparison.Result;
    var pull = content.Links.Pull(content.Pull.Number);
    ImpactedFile[] files =
    [
      .. comparison.Files
                   .Where(static file => file.MissingPatchLines > 0 || IsRemoved(file))
                   .OrderByDescending(static file => file.MissingPatchLines)
                   .ThenByDescending(static file => Math.Abs(file.Delta ?? 0))
                   .ThenBy(static file => file.Path, StringComparer.Ordinal),
    ];
    var indirect = comparison.IndirectChanges.Count;

    if (files.Length == 0)
    {
      return indirect == 0 ? [] : [CommentFormatting.Link($"see {CountIndirect(indirect)}", pull)];
    }

    var lines = new List<string>
    {
      $"| {CommentFormatting.Link("Files with missing lines", pull)} | Coverage \u0394 | |",
      "|---|---|---|",
    };

    lines.AddRange(files.Take(limit).Select(file => WriteRow(file, content)));

    if (files.Length > limit)
    {
      lines.Add($"| ... and {CommentFormatting.Link($"{CommentFormatting.Number(files.Length - limit)} more", pull)} | | |");
    }

    if (indirect > 0)
    {
      lines.Add($"| ... and {CommentFormatting.Link(CountIndirect(indirect), pull)} | | |");
    }

    return lines;
  }

  /// <summary>Tells whether a file is removed by the pull request and was covered before.</summary>
  /// <param name="file">The file.</param>
  /// <returns><see langword="true" /> for a removed file the base holds.</returns>
  private static bool IsRemoved(ImpactedFile file) => file is { Status: FileDiffStatus.Removed, Head: null, Base: not null };

  /// <summary>Names a number of files with indirect changes.</summary>
  /// <param name="count">The number of files.</param>
  /// <returns>The text.</returns>
  private static string CountIndirect(int count) => $"{CommentFormatting.Count(count, "file")} with indirect coverage changes";

  /// <summary>Writes the row of one file.</summary>
  /// <param name="file">The file.</param>
  /// <param name="content">What the comment is written from.</param>
  /// <returns>The row.</returns>
  private static string WriteRow(ImpactedFile file, CommentContent content)
  {
    var path = CommentFormatting.Escape(file.Path);

    if (file.Head is not { } head)
    {
      return $"| ~~{path}~~ | | |";
    }

    var link = CommentFormatting.Link(path, content.Links.File(content.Head.Sha, file.Path, content.Pull.Number));
    var metrics = CommentFormatting.Metrics(head, file.Base, file.Patch.Coverage, content.Config, MissingBase.NoImpact);
    var arrow = CommentFormatting.Arrow(file.Delta);

    return arrow.Length == 0 ? $"| {link} | {metrics} | |" : $"| {link} | {metrics} | {arrow} |";
  }

  #endregion
}
