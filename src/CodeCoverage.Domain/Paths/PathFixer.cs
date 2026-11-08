// ------------------------------------------------------------------------------------------------
// <copyright file="PathFixer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Paths;

/// <summary>Turns the paths of a coverage report into paths of the repository.</summary>
public sealed class PathFixer
{
  #region Fields

  private const string FileScheme = "file://";

  private readonly PathFixContext _context;

  private readonly string? _rootPrefix;

  private readonly string[] _baseDirectories;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="PathFixer" /> class.</summary>
  /// <param name="context">What is known about the report.</param>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is null.</exception>
  public PathFixer(PathFixContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    var root = context.Root is null ? string.Empty : Normalize(context.Root);

    _context = context;
    _rootPrefix = root.Length == 0 ? null : root + "/";
    _baseDirectories = [.. ListBaseDirectories(context)];
  }

  #endregion

  #region Methods

  /// <summary>Puts a path in the canonical form: relative, with forward slashes, without dots.</summary>
  /// <param name="path">The path, as written in a report.</param>
  /// <returns>The normalized path, which is empty if nothing remains of it.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
  public static string Normalize(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    var value = path.Trim();
    var isFileUri = value.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase);

    if (isFileUri)
    {
      value = value[FileScheme.Length..];
    }

    value = value.Replace('\\', '/');

    if (isFileUri && value.StartsWith('/') && HasDriveLetter(value.AsSpan(1)))
    {
      value = value[1..];
    }

    if (HasDriveLetter(value))
    {
      value = value[2..];
    }

    var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var isDeterministic = value.StartsWith('/') && segments.Length > 1 && segments[0] == "_";

    return string.Join('/', ResolveDots(segments.AsSpan(isDeterministic ? 1 : 0)));
  }

  /// <summary>Applies the <c>fixes</c> of the configuration to a normalized path, once.</summary>
  /// <param name="path">The normalized path.</param>
  /// <param name="addPrefixes">
  ///   <see langword="true" /> to add the <c>::prefix</c> entries, which Codecov only does on the
  ///   resolved path; <see langword="false" /> before the resolution.
  /// </param>
  /// <returns>The path, with the replacement of the first entry that matches its start.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
  public string ApplyFixes(string path, bool addPrefixes)
  {
    ArgumentNullException.ThrowIfNull(path);

    return _context.Fixes.Apply(path, addPrefixes);
  }

  /// <summary>Lists the repository paths a report path may stand for, the likeliest first.</summary>
  /// <param name="path">The path, as written in a report.</param>
  /// <returns>
  ///   The path itself; then, for a relative path and with a list of files, the path under the
  ///   report directory and under each Cobertura source; then the path without its CI prefix. With
  ///   a list of files and <c>disable_default_path_fixes</c>, only the path itself.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
  /// <remarks>
  ///   Like Codecov, a base directory is joined before the normalization and the <c>fixes</c>, and
  ///   the report directory comes before the Cobertura sources.
  /// </remarks>
  public IReadOnlyList<string> Candidates(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    var prepared = Prepare(path);

    if (prepared.Length == 0)
    {
      return [];
    }

    var hasNetwork = _context.Network is { IsEmpty: false };

    if (hasNetwork && _context.DisableDefaultPathFixes)
    {
      return [prepared];
    }

    var candidates = new List<string> { prepared };

    if (hasNetwork && !IsAbsolute(path))
    {
      foreach (var directory in _baseDirectories)
      {
        AddCandidate(candidates, Prepare($"{directory}/{path.Trim()}"));
      }
    }

    AddCandidate(candidates, CiPathPrefixes.Strip(prepared));

    return candidates;
  }

  private static IEnumerable<string> ListBaseDirectories(PathFixContext context)
  {
    if (context.ReportPath is { } reportPath)
    {
      var slash = reportPath.Replace('\\', '/').LastIndexOf('/');

      if (slash > 0)
      {
        yield return reportPath[..slash];
      }
    }

    foreach (var sourceRoot in context.SourceRoots.Where(root => !string.IsNullOrWhiteSpace(root)))
    {
      yield return sourceRoot;
    }
  }

  private static bool HasDriveLetter(ReadOnlySpan<char> value) =>
    value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':';

  private static List<string> ResolveDots(ReadOnlySpan<string> segments)
  {
    var resolved = new List<string>(segments.Length);

    foreach (var segment in segments)
    {
      if (segment == "..")
      {
        if (resolved.Count > 0)
        {
          resolved.RemoveAt(resolved.Count - 1);
        }
      }
      else if (segment != ".")
      {
        resolved.Add(segment);
      }
    }

    return resolved;
  }

  private static bool IsAbsolute(string path)
  {
    var value = path.TrimStart();

    return value.StartsWith('/')
      || value.StartsWith('\\')
      || value.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase)
      || HasDriveLetter(value);
  }

  private static void AddCandidate(List<string> candidates, string? candidate)
  {
    if (!string.IsNullOrEmpty(candidate) && !candidates.Contains(candidate))
    {
      candidates.Add(candidate);
    }
  }

  private string Prepare(string path) => StripRoot(ApplyFixes(Normalize(path), addPrefixes: false));

  private string StripRoot(string path) =>
    _rootPrefix is not null && path.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase)
      ? path[_rootPrefix.Length..]
      : path;

  #endregion
}
