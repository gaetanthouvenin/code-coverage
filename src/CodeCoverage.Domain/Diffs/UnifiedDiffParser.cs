// ------------------------------------------------------------------------------------------------
// <copyright file="UnifiedDiffParser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeCoverage.Domain.Diffs;

/// <summary>Reads the unified diff of one file, as the <c>patch</c> field of the GitHub API gives it.</summary>
public static partial class UnifiedDiffParser
{
  #region Methods

  /// <summary>Reads the blocks of changes of a patch.</summary>
  /// <param name="patch">The patch of one file; what comes before its first block is skipped.</param>
  /// <returns>The blocks, in the order of the patch; none when the patch is empty.</returns>
  /// <remarks>
  ///   <para>
  ///     The body of a block is delimited by the counts of its header, so a removed <c>-- comment</c>
  ///     (written <c>--- comment</c>) is a removed line. Codecov takes such a line, and an added
  ///     <c>++ x</c>, for a file header and skips it, which shifts the rest of its block: this is a
  ///     documented difference.
  ///   </para>
  ///   <para>
  ///     Lines are separated by line feeds only, as Git counts them: a carriage return inside a line
  ///     does not start another one.
  ///   </para>
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="patch" /> is <see langword="null" />.</exception>
  /// <exception cref="FormatException">
  ///   A block header is not valid, or a body does not match the counts of its header.
  /// </exception>
  public static IReadOnlyList<Hunk> Parse(string patch)
  {
    ArgumentNullException.ThrowIfNull(patch);

    var lines = patch.Split('\n');
    var hunks = new List<Hunk>();
    var index = 0;

    while (index < lines.Length)
    {
      if (lines[index].StartsWith("@@", StringComparison.Ordinal))
      {
        hunks.Add(ReadHunk(lines, ref index));
      }
      else
      {
        index++;
      }
    }

    return hunks;
  }

  /// <summary>Gets the expression of a block header, <c>@@ -start[,count] +start[,count] @@</c>.</summary>
  /// <returns>The expression.</returns>
  [GeneratedRegex(@"\A@@ -([0-9]+)(?:,([0-9]+))? \+([0-9]+)(?:,([0-9]+))? @@", RegexOptions.CultureInvariant)]
  private static partial Regex HunkHeader();

  /// <summary>Reads one block.</summary>
  /// <param name="lines">The lines of the patch.</param>
  /// <param name="index">The index of the header; on return, the index of the line after the block.</param>
  /// <returns>The block.</returns>
  /// <exception cref="FormatException">The header or the body is not valid.</exception>
  private static Hunk ReadHunk(string[] lines, ref int index)
  {
    var header = HunkHeader().Match(lines[index]);

    if (!header.Success)
    {
      throw new FormatException($"Line {index + 1} of the patch is not a valid hunk header.");
    }

    var oldStart = ReadNumber(header.Groups[1]);
    var oldCount = ReadNumber(header.Groups[2]);
    var newStart = ReadNumber(header.Groups[3]);
    var newCount = ReadNumber(header.Groups[4]);

    // As in Codecov (`int(header[2]) or 1`): a start of 0 only comes with an empty side.
    var oldLine = Math.Max(oldStart, 1);
    var newLine = Math.Max(newStart, 1);
    var oldEnd = oldLine + oldCount;
    var newEnd = newLine + newCount;
    var removed = new List<int>();
    var added = new List<int>();

    index++;

    while (oldLine < oldEnd || newLine < newEnd)
    {
      if (index >= lines.Length)
      {
        throw new FormatException("The patch ends inside a hunk.");
      }

      var prefix = lines[index].Length > 0 ? lines[index][0] : '\0';

      switch (prefix)
      {
        case ' ' when oldLine < oldEnd && newLine < newEnd:
          oldLine++;
          newLine++;
          break;
        case '-' when oldLine < oldEnd:
          removed.Add(oldLine++);
          break;
        case '+' when newLine < newEnd:
          added.Add(newLine++);
          break;
        case '\\':
          break;
        default:
          throw new FormatException($"Line {index + 1} of the patch does not match the counts of its hunk.");
      }

      index++;
    }

    return new Hunk(oldStart, oldCount, newStart, newCount, removed, added);
  }

  /// <summary>Reads a number of a block header.</summary>
  /// <param name="group">The captured number; an absent count means one line.</param>
  /// <returns>The number.</returns>
  /// <exception cref="FormatException">The number is too large.</exception>
  private static int ReadNumber(Group group)
  {
    if (!group.Success)
    {
      return 1;
    }

    return int.TryParse(group.ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
      ? number
      : throw new FormatException("A number of a hunk header is too large.");
  }

  #endregion
}
