// ------------------------------------------------------------------------------------------------
// <copyright file="PathPattern.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;

namespace CodeCoverage.Domain.Paths;

/// <summary>A <c>codecov.yml</c> pattern, matched against a path or a branch name.</summary>
/// <remarks>
///   <para>
///     Patterns come from user-supplied files: they run without backtracking, in a time linear in
///     the length of the value. A pattern that cannot run this way is refused rather than rejected:
///     it stays in its set, it matches nothing and <see cref="IsSupported" /> is
///     <see langword="false" />.
///   </para>
///   <para>
///     There is deliberately no match timeout. The engine reads the clock while it builds its
///     automaton, so a timeout turned a preempted thread into a wrong answer, which is not an answer
///     Codecov can give. The ceiling is the timeout of the job.
///   </para>
/// </remarks>
public sealed class PathPattern
{
  #region Fields

  private const RegexOptions CompileOptions =
    RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

  private static readonly TimeSpan MatchTimeout = Regex.InfiniteMatchTimeout;

  private static readonly char[] ReservedCharacters = ['*', '$', '[', ']'];

  private readonly Regex? _regex;

  #endregion

  #region Ctors

  private PathPattern(string source, bool isNegative, Regex? regex)
  {
    Source = source;
    IsNegative = isNegative;
    _regex = regex;
  }

  #endregion

  #region Properties

  /// <summary>Gets the pattern, as written in the configuration.</summary>
  public string Source { get; }

  /// <summary>Gets a value indicating whether the pattern excludes what it matches.</summary>
  public bool IsNegative { get; }

  /// <summary>Gets a value indicating whether the pattern can run without backtracking.</summary>
  public bool IsSupported => _regex is not null;

  #endregion

  #region Methods

  /// <summary>Reads a pattern of an <c>ignore</c> or <c>paths</c> list.</summary>
  /// <param name="pattern">The pattern, as written in the configuration.</param>
  /// <returns>The pattern, recognized as a prefix, a glob or a regular expression.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="pattern" /> is null.</exception>
  /// <remarks>The recognition is the one of Codecov, ambiguities included.</remarks>
  public static PathPattern Parse(string pattern)
  {
    ArgumentNullException.ThrowIfNull(pattern);

    var body = pattern.TrimStart('!');
    var isNegative = body.Length != pattern.Length;

    if (body.StartsWith("./", StringComparison.Ordinal))
    {
      body = body[2..];
    }

    return new PathPattern(pattern, isNegative, CompilePathBody(body));
  }

  /// <summary>Reads a pattern of a <c>branches</c> list.</summary>
  /// <param name="pattern">The pattern, as written in the configuration.</param>
  /// <returns>The pattern, which matches a whole name unless it ends with <c>.*</c>.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="pattern" /> is null.</exception>
  /// <remarks>
  ///   A branch pattern is neither a prefix nor a glob: <c>main</c> only matches <c>main</c>, and
  ///   the <c>*</c> of <c>feature/*</c> crosses the slashes.
  /// </remarks>
  public static PathPattern ParseBranch(string pattern)
  {
    ArgumentNullException.ThrowIfNull(pattern);

    var body = ToBranchRegex(pattern);
    var isNegative = body.StartsWith("^!", StringComparison.Ordinal);

    if (isNegative)
    {
      body = string.Concat("^", body.AsSpan().TrimStart("^!"));
    }

    return new PathPattern(
      pattern,
      isNegative,
      TryCompileStartAnchored(body, out var regex) ? regex : null);
  }

  /// <summary>Gets the opposite pattern, which is how an <c>ignore</c> entry joins a set.</summary>
  /// <returns>The same pattern, with the opposite <see cref="IsNegative" />.</returns>
  public PathPattern Invert() => new(Source, !IsNegative, _regex);

  /// <summary>Tests the pattern, regardless of <see cref="IsNegative" />.</summary>
  /// <param name="path">The repository-relative path, or the branch name.</param>
  /// <returns>
  ///   <see langword="true" /> if the pattern matches; <see langword="false" /> otherwise, or if it
  ///   is refused.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
  public bool Match(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    return _regex is not null && _regex.IsMatch(path);
  }

  /// <summary>Tests whether a pattern body is recognized as a prefix.</summary>
  /// <param name="body">The pattern, without its <c>!</c> and <c>./</c> markers.</param>
  /// <returns><see langword="true" /> if it has none of the reserved characters.</returns>
  internal static bool IsPrefix(string body) => body.IndexOfAny(ReservedCharacters) < 0;

  /// <summary>Tests whether a pattern body with reserved characters is recognized as a glob.</summary>
  /// <param name="body">The pattern, without its <c>!</c> and <c>./</c> markers.</param>
  /// <returns><see langword="true" /> if it is a glob; otherwise, it may be a regular expression.</returns>
  internal static bool IsGlob(string body) =>
    body.Contains("**", StringComparison.Ordinal)
    || body.Contains("/*", StringComparison.Ordinal)
    || (body.Contains('*')
      && !body.Contains("]*", StringComparison.Ordinal)
      && !body.Contains(".*", StringComparison.Ordinal));

  /// <summary>Translates a glob, as <c>translate_glob_to_regex</c> of Codecov does.</summary>
  /// <param name="glob">The glob.</param>
  /// <returns>The regular expression, without any anchor.</returns>
  internal static string TranslateGlobBody(string glob)
  {
    var expression = new StringBuilder("(?s:");
    var index = 0;

    while (index < glob.Length)
    {
      var current = glob[index++];

      switch (current)
      {
        case '*' when index < glob.Length && glob[index] == '*':
          expression.Append(".*");
          index++;
          break;
        case '*':
          expression.Append("[^/]*");
          break;
        case '?':
          expression.Append('.');
          break;
        case '[':
          index = AppendCharacterClass(expression, glob, index);
          break;
        default:
          expression.Append(Regex.Escape(new string(current, 1)));
          break;
      }
    }

    return expression.Append(')').ToString();
  }

  private static Regex? CompilePathBody(string body)
  {
    if (IsPrefix(body))
    {
      return TryCompileStartAnchored(body, out var prefix)
        ? prefix
        : CompileOrRefuse(@"\A" + Regex.Escape(body));
    }

    if (!IsGlob(body) && TryCompileStartAnchored(body, out var regex))
    {
      return regex;
    }

    return CompileOrRefuse($@"\A{TranslateGlobBody(body)}\z");
  }

  // Returns false on a syntax error. A refused construct returns true with a null regex: it is
  // valid, it only cannot run here. The body is compiled alone first, so that a stray parenthesis
  // cannot close the anchoring group.
  private static bool TryCompileStartAnchored(string body, out Regex? regex)
  {
    regex = null;

    try
    {
      _ = new Regex(body, CompileOptions);
      regex = new Regex($@"\A(?:{body})", CompileOptions, MatchTimeout);

      return true;
    }
    catch (NotSupportedException)
    {
      return true;
    }
    catch (ArgumentException)
    {
      return false;
    }
  }

  private static Regex? CompileOrRefuse(string expression)
  {
    try
    {
      return new Regex(expression, CompileOptions, MatchTimeout);
    }
    catch (NotSupportedException)
    {
      return null;
    }
    catch (ArgumentException)
    {
      return null;
    }
  }

  // The index is just after the opening bracket; the result is where the translation resumes.
  private static int AppendCharacterClass(StringBuilder expression, string glob, int index)
  {
    var end = index;

    if (end < glob.Length && glob[end] == '!')
    {
      end++;
    }

    if (end < glob.Length && glob[end] == ']')
    {
      end++;
    }

    end = end < glob.Length ? glob.IndexOf(']', end) : -1;

    if (end < 0)
    {
      expression.Append(@"\[");

      return index;
    }

    expression.Append('[');

    for (var position = index; position < end; position++)
    {
      var current = glob[position];

      if (position == index && current == '!')
      {
        expression.Append('^');
      }
      else if ((position == index && current == '^') || current is '\\' or '[' or ']')
      {
        expression.Append('\\').Append(current);
      }
      else
      {
        expression.Append(current);
      }
    }

    expression.Append(']');

    return end + 1;
  }

  private static string ToBranchRegex(string pattern)
  {
    if (pattern is "" or "*" or ".*")
    {
      return ".*";
    }

    var expression = new StringBuilder();
    var value = pattern.Trim();

    for (var index = 0; index < value.Length; index++)
    {
      if (value[index] == '*' && (index == 0 || value[index - 1] != '.'))
      {
        expression.Append(".*");
      }
      else
      {
        expression.Append(value[index]);
      }
    }

    var body = expression.ToString();

    if (!body.StartsWith(".*", StringComparison.Ordinal) && !body.StartsWith('^'))
    {
      body = "^" + body;
    }

    if (!body.EndsWith(".*", StringComparison.Ordinal) && !body.EndsWith('$'))
    {
      body += "$";
    }

    return body;
  }

  #endregion
}
