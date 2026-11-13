// ------------------------------------------------------------------------------------------------
// <copyright file="AnnotatedSource.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Source;

/// <summary>
///   A source file at a commit, read live from its forge and never stored, merged line by line with the coverage of the
///   report. When the source cannot be shown, the totals of the file remain.
/// </summary>
/// <param name="Path">The path of the file in the report.</param>
/// <param name="Sha">The SHA of the commit.</param>
/// <param name="Flag">The flag the coverage is restricted to, or <see langword="null" /> for every session.</param>
/// <param name="Totals">The totals of the file.</param>
/// <param name="IsSourceAvailable">
///   <see langword="false" /> when the forge does not have the file at this commit, refused or rate limited the reading,
///   or the file is too large or binary: <paramref name="Lines" /> is then empty.
/// </param>
/// <param name="Lines">The lines of the source, in order.</param>
public sealed record AnnotatedSource(
  string Path,
  GitSha Sha,
  string? Flag,
  CoverageTotals Totals,
  bool IsSourceAvailable,
  IReadOnlyList<AnnotatedLine> Lines)
{
  #region Fields

  private const char ByteOrderMark = '\uFEFF';

  #endregion

  #region Methods

  /// <summary>Merges the text of a source file with the coverage of its lines.</summary>
  /// <param name="file">The coverage of the file in the report.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="flag">The flag the coverage is restricted to, or <see langword="null" />.</param>
  /// <param name="text">The text read from the forge, or <see langword="null" /> when it cannot be shown.</param>
  /// <returns>
  ///   The source: a byte order mark is dropped, <c>\n</c> and <c>\r\n</c> both end a line, a final end of line opens
  ///   no extra line, and a line of the report beyond the end of the text is ignored. A text holding a NUL character is
  ///   binary and is not shown.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="file" /> or <paramref name="sha" /> is <see langword="null" />.</exception>
  public static AnnotatedSource Merge(FileCoverage file, GitSha sha, string? flag, string? text)
  {
    ArgumentNullException.ThrowIfNull(file);
    ArgumentNullException.ThrowIfNull(sha);

    if (text is null || text.Contains('\0', StringComparison.Ordinal))
    {
      return new AnnotatedSource(file.Path, sha, flag, file.Totals, false, []);
    }

    var source = text.Length > 0 && text[0] == ByteOrderMark ? text[1..] : text;
    var texts = source.Split('\n');
    var count = texts.Length > 1 && texts[^1].Length == 0 ? texts.Length - 1 : texts.Length;
    var lines = new AnnotatedLine[source.Length == 0 ? 0 : count];

    // The lines of the report are sorted by number: they are walked in step with the text.
    var covered = file.Lines;
    var next = 0;

    for (var index = 0; index < lines.Length; index++)
    {
      var number = index + 1;
      var content = texts[index].EndsWith('\r') ? texts[index][..^1] : texts[index];

      while (next < covered.Count && covered[next].Line < number)
      {
        next++;
      }

      lines[index] = next < covered.Count && covered[next].Line == number
                       ? new AnnotatedLine(number, content, CoverageMath.Classify(covered[next]), covered[next].Hits, covered[next].Covered, covered[next].Total)
                       : new AnnotatedLine(number, content, null, null, 0, 0);
    }

    return new AnnotatedSource(file.Path, sha, flag, file.Totals, true, lines);
  }

  #endregion
}
