// ------------------------------------------------------------------------------------------------
// <copyright file="GlobMatcher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Discovery;

/// <summary>Matches relative paths against a glob.</summary>
/// <remarks>
///   <para>
///     The syntax is the one of the Codecov CLI: <c>*</c> and <c>?</c> within a segment, a whole
///     <c>**</c> segment across any number of segments, <c>[abc]</c>, <c>[a-z]</c> and
///     <c>[!abc]</c>. A <c>[</c> without its closing bracket is a literal.
///   </para>
///   <para>
///     The result never depends on the platform: the case always matters, as in the Codecov CLI,
///     and a backslash is always a separator, so that a pattern written for one platform works on
///     the other.
///   </para>
/// </remarks>
internal sealed class GlobMatcher
{
  #region Fields

  private const char Separator = '/';
  private const char AnyCharacters = '*';
  private const char AnyCharacter = '?';
  private const char ClassStart = '[';
  private const char ClassEnd = ']';
  private const char ClassNegation = '!';
  private const char ClassRange = '-';
  private const string AnySegments = "**";
  private const string CurrentDirectory = ".";

  private static readonly char[] Wildcards = [AnyCharacters, AnyCharacter, ClassStart];

  private readonly string[] _segments;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="GlobMatcher" /> class.</summary>
  /// <param name="pattern">The glob.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public GlobMatcher(string pattern)
  {
    ArgumentNullException.ThrowIfNull(pattern);

    _segments = SplitSegments(NormalizeSeparators(pattern))
                .Where(segment => segment is not CurrentDirectory)
                .ToArray();
  }

  #endregion

  #region Methods

  /// <summary>Replaces the backslashes of a pattern or path by forward slashes.</summary>
  /// <param name="value">The pattern or path.</param>
  /// <returns>The value, with forward slashes only.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static string NormalizeSeparators(string value)
  {
    ArgumentNullException.ThrowIfNull(value);

    return value.Replace('\\', Separator);
  }

  /// <summary>Finds the first wildcard of a value.</summary>
  /// <param name="value">The pattern or path.</param>
  /// <returns>The index of the first wildcard; a negative number when there is none.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static int IndexOfWildcard(string value)
  {
    ArgumentNullException.ThrowIfNull(value);

    return value.IndexOfAny(Wildcards);
  }

  /// <summary>Tests a path against the glob.</summary>
  /// <param name="relativePath">The path, relative to the directory the glob starts from.</param>
  /// <returns><see langword="true" /> when the path matches.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public bool IsMatch(string relativePath)
  {
    ArgumentNullException.ThrowIfNull(relativePath);

    return MatchSegments(_segments, SplitSegments(relativePath));
  }

  private static string[] SplitSegments(string path)
  {
    return path.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
  }

  // Only the last ** is ever backtracked to: whatever an earlier one matched, it could have left to
  // the later one. The work stays proportional to the length of the path times the one of the glob.
  private static bool MatchSegments(string[] glob, string[] path)
  {
    var globIndex = 0;
    var pathIndex = 0;
    var lastAnySegments = -1;
    var resumeAt = 0;

    while (pathIndex < path.Length)
    {
      if (globIndex < glob.Length && glob[globIndex] is AnySegments)
      {
        lastAnySegments = globIndex++;
        resumeAt = pathIndex;
      }
      else if (globIndex < glob.Length && MatchSegment(glob[globIndex], path[pathIndex]))
      {
        globIndex++;
        pathIndex++;
      }
      else if (lastAnySegments >= 0)
      {
        globIndex = lastAnySegments + 1;
        pathIndex = ++resumeAt;
      }
      else
      {
        return false;
      }
    }

    // A last ** is left out on purpose: "a/**" names what is below a, not a itself.
    while (globIndex < glob.Length - 1 && glob[globIndex] is AnySegments)
    {
      globIndex++;
    }

    return globIndex == glob.Length;
  }

  private static bool MatchSegment(ReadOnlySpan<char> glob, ReadOnlySpan<char> name)
  {
    var globIndex = 0;
    var nameIndex = 0;
    var lastStar = -1;
    var resumeAt = 0;

    while (nameIndex < name.Length)
    {
      if (globIndex < glob.Length && glob[globIndex] is AnyCharacters)
      {
        lastStar = globIndex++;
        resumeAt = nameIndex;
      }
      else if (globIndex < glob.Length
               && MatchCharacter(glob, globIndex, name[nameIndex], out var consumed))
      {
        globIndex += consumed;
        nameIndex++;
      }
      else if (lastStar >= 0)
      {
        globIndex = lastStar + 1;
        nameIndex = ++resumeAt;
      }
      else
      {
        return false;
      }
    }

    while (globIndex < glob.Length && glob[globIndex] is AnyCharacters)
    {
      globIndex++;
    }

    return globIndex == glob.Length;
  }

  private static bool MatchCharacter(
    ReadOnlySpan<char> glob,
    int index,
    char character,
    out int consumed)
  {
    consumed = 1;

    if (glob[index] is AnyCharacter)
    {
      return true;
    }

    if (glob[index] is ClassStart && FindClassEnd(glob, index) is var end and >= 0)
    {
      consumed = end - index + 1;

      return ClassContains(glob[(index + 1)..end], character);
    }

    return glob[index] == character;
  }

  // A ] right after the opening is a member of the class, not its end.
  private static int FindClassEnd(ReadOnlySpan<char> glob, int start)
  {
    var index = start + 1;

    if (index < glob.Length && glob[index] is ClassNegation)
    {
      index++;
    }

    if (index < glob.Length && glob[index] is ClassEnd)
    {
      index++;
    }

    var offset = glob[index..].IndexOf(ClassEnd);

    return offset < 0 ? -1 : index + offset;
  }

  // A reversed range such as z-a contains nothing, as in the Codecov CLI.
  private static bool ClassContains(ReadOnlySpan<char> members, char character)
  {
    var isNegated = members[0] is ClassNegation;
    var index = isNegated ? 1 : 0;
    var contains = false;

    while (index < members.Length && !contains)
    {
      if (index + 2 < members.Length && members[index + 1] is ClassRange)
      {
        contains = members[index] <= character && character <= members[index + 2];
        index += 3;
      }
      else
      {
        contains = members[index] == character;
        index++;
      }
    }

    return contains != isNegated;
  }

  #endregion
}
