// ------------------------------------------------------------------------------------------------
// <copyright file="FixRule.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeCoverage.Domain.Paths;

/// <summary>The <c>fixes</c> of a <c>codecov.yml</c>, compiled into one anchored alternation.</summary>
/// <remarks>
///   <para>
///     An entry is read as <c>UserPathFixes</c> and <c>CustomFixPathSchemaField</c> of Codecov read
///     it, quirks included: a <c>before::after</c> entry is recognized as a prefix, a glob or a
///     regular expression, a <c>before::</c> entry is not, and both then have their stars expanded.
///     A single <c>*</c> of a <c>before::after</c> glob therefore needs two characters, and a prefix
///     that starts with <c>/</c> and has a replacement never matches a normalized path.
///   </para>
///   <para>
///     Two differences with Codecov are deliberate. The rules are tried in the order of the file,
///     because Codecov's order comes from a Python <c>set</c> and changes between processes. Every
///     alternative is anchored, because Codecov only anchors the first one of that random order.
///   </para>
///   <para>
///     Entries run without backtracking, in a time linear in the length of the path, and without a
///     match timeout, for the reason given by <see cref="PathPattern" />. An entry that cannot run
///     this way, or that does not compile, is left out and reported in <see cref="RefusedFixes" />.
///   </para>
/// </remarks>
public sealed class FixRule
{
  #region Fields

  private const string Separator = "::";

  private const RegexOptions CompileOptions =
    RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

  private static readonly TimeSpan MatchTimeout = Regex.InfiniteMatchTimeout;

  private static readonly Regex SingleStar = new(@"(?<!\.)\*", RegexOptions.CultureInvariant);

  private readonly string _prefix;

  private readonly Regex? _alternation;

  private readonly int[] _groups;

  private readonly string[] _replacements;

  #endregion

  #region Ctors

  private FixRule(
    string prefix,
    Regex? alternation,
    int[] groups,
    string[] replacements,
    string[] refusedFixes)
  {
    _prefix = prefix;
    _alternation = alternation;
    _groups = groups;
    _replacements = replacements;
    RefusedFixes = refusedFixes;
  }

  #endregion

  #region Properties

  /// <summary>Gets the rule of a configuration without any entry.</summary>
  public static FixRule None { get; } = new(string.Empty, null, [], [], []);

  /// <summary>Gets the entries that are left out, as written in the configuration.</summary>
  public IReadOnlyList<string> RefusedFixes { get; }

  #endregion

  #region Methods

  /// <summary>Compiles the entries of a <c>fixes</c> list.</summary>
  /// <param name="fixes">The entries, as written in the configuration.</param>
  /// <returns>The compiled rule.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="fixes" /> is null.</exception>
  public static FixRule Parse(IEnumerable<string> fixes)
  {
    ArgumentNullException.ThrowIfNull(fixes);

    var prefixes = new List<string>();
    var accepted = new List<string>();
    var expressions = new List<string>();
    var replacements = new List<string>();
    var refused = new List<string>();

    foreach (var fix in fixes.Distinct(StringComparer.Ordinal))
    {
      if (fix.StartsWith(Separator, StringComparison.Ordinal))
      {
        prefixes.Add(fix[Separator.Length..].TrimEnd('/'));
      }
      else if (TryTranslate(fix, out var expression, out var replacement))
      {
        accepted.Add(fix);
        expressions.Add(expression);
        replacements.Add(replacement);
      }
      else
      {
        refused.Add(fix);
      }
    }

    var prefix = string.Join('/', prefixes);

    if (expressions.Count == 0)
    {
      return new FixRule(prefix, null, [], [], [.. refused]);
    }

    var alternation = Compile(ToAlternation(expressions));

    if (alternation is null)
    {
      return new FixRule(prefix, null, [], [], [.. refused, .. accepted]);
    }

    int[] groups = [.. expressions.Select((_, index) => alternation.GroupNumberFromName(GroupName(index)))];

    return new FixRule(prefix, alternation, groups, [.. replacements], [.. refused]);
  }

  /// <summary>Applies the rule to a path, once.</summary>
  /// <param name="path">The normalized path.</param>
  /// <param name="addPrefixes">
  ///   <see langword="true" /> to add the <c>::prefix</c> entries before the substitution. Codecov
  ///   applies the substitution before the resolution without them, then again after it with them.
  /// </param>
  /// <returns>The path, with the replacement of the first entry that matches its start.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
  public string Apply(string path, bool addPrefixes)
  {
    ArgumentNullException.ThrowIfNull(path);

    var prefixed = addPrefixes && _prefix.Length > 0 ? $"{_prefix}/{path}" : path;

    return Substitute(prefixed).Replace("//", "/", StringComparison.Ordinal).TrimStart('/');
  }

  private static bool TryTranslate(string fix, out string expression, out string replacement)
  {
    expression = string.Empty;
    replacement = string.Empty;

    var parts = fix.Split(Separator);

    if (parts.Length < 2)
    {
      return false;
    }

    var isRemoval = fix.Length == parts[0].Length + Separator.Length;
    var before = RemoveDriveLetter(parts[0]);

    expression = ExpandStars(isRemoval ? before : Regexify(before));
    replacement = parts[1];

    return Compile(expression) is not null;
  }

  // The normalization removes the drive letter of the paths, so the rules must not expect it.
  private static string RemoveDriveLetter(string before) =>
    before.Length >= 2 && char.IsAsciiLetter(before[0]) && before[1] == ':'
      ? before[2..].TrimStart('/')
      : before;

  private static string Regexify(string before)
  {
    if (PathPattern.IsPrefix(before))
    {
      return "^" + before;
    }

    return !PathPattern.IsGlob(before) && IsRegex(before)
      ? before
      : PathPattern.TranslateGlobBody(before);
  }

  private static bool IsRegex(string value)
  {
    try
    {
      _ = new Regex(value, RegexOptions.CultureInvariant);

      return true;
    }
    catch (ArgumentException)
    {
      return false;
    }
  }

  private static string ExpandStars(string expression) =>
    SingleStar.Replace(expression.Replace("**", ".*", StringComparison.Ordinal), @"[^/\n]+")
      .TrimStart('/');

  private static string ToAlternation(List<string> expressions) =>
    $@"\A(?:{string.Join('|', expressions.Select((expression, index) => $"(?<{GroupName(index)}>{expression})"))})";

  private static string GroupName(int index) =>
    string.Create(CultureInfo.InvariantCulture, $"fix{index}");

  private static Regex? Compile(string expression)
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

  private string Substitute(string path)
  {
    if (_alternation is null)
    {
      return path;
    }

    var match = _alternation.Match(path);

    if (!match.Success || match.Length == 0)
    {
      return path;
    }

    var rule = Array.FindIndex(_groups, group => match.Groups[group].Success);

    return string.Concat(_replacements[rule], path.AsSpan(match.Length));
  }

  #endregion
}
