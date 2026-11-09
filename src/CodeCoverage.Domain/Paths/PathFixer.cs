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

  private const int MinimumSuffixScore = 2;

  private readonly PathFixContext _context;

  private readonly string? _rootPrefix;

  private readonly string? _reportDirectoryPrefix;

  private readonly string[] _baseDirectories;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="PathFixer" /> class.</summary>
  /// <param name="context">What is known about the report.</param>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is null.</exception>
  public PathFixer(PathFixContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    _context = context;
    _rootPrefix = ToDirectoryPrefix(context.Root is null ? string.Empty : Normalize(context.Root));
    _baseDirectories = [.. ListBaseDirectories(context)];

    if (GetReportDirectory(context.ReportPath) is { } reportDirectory)
    {
      _reportDirectoryPrefix = ToDirectoryPrefix(StripRoot(Normalize(reportDirectory)));
    }
  }

  #endregion

  #region Properties

  /// <summary>Gets the <c>fixes</c> left out of the configuration, for the processing log.</summary>
  public IReadOnlyList<string> RefusedFixes => _context.Fixes.RefusedFixes;

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

  /// <summary>Turns a path of a report into a path of the repository.</summary>
  /// <param name="path">The path, as written in a report.</param>
  /// <returns>
  ///   The retained path, once the <c>fixes</c> have run on it again with the <c>::prefix</c>
  ///   entries; or the reason why the path is dropped.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
  /// <remarks>
  ///   <para>
  ///     As with the <c>ancestors=1</c> of Codecov, a file only matches by suffix when two segments
  ///     are equal, except for a file at the root of the repository and for a path that is a bare
  ///     file name, unique in the repository.
  ///   </para>
  ///   <para>
  ///     Unlike Codecov, which drops a path when its candidates branch out, a tie is broken by the
  ///     <c>paths</c> of the flags, then by the report directory, then by the shortest path.
  ///   </para>
  ///   <para>
  ///     With <c>disable_default_path_fixes</c> and a list of files, the path is resolved as it
  ///     stands, even if the list does not have it.
  ///   </para>
  /// </remarks>
  public PathFixResult Fix(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    var candidates = Candidates(path);

    if (candidates.Count == 0)
    {
      return PathFixResult.Unresolved;
    }

    if (_context.Network is not { IsEmpty: false } network)
    {
      return Retain(candidates[^1], PathFixResult.Unverified);
    }

    return _context.DisableDefaultPathFixes
      ? Retain(candidates[0], PathFixResult.Resolved)
      : Resolve(network, candidates);
  }

  private static IEnumerable<string> ListBaseDirectories(PathFixContext context)
  {
    if (GetReportDirectory(context.ReportPath) is { } reportDirectory)
    {
      yield return reportDirectory;
    }

    foreach (var sourceRoot in context.SourceRoots.Where(root => !string.IsNullOrWhiteSpace(root)))
    {
      yield return sourceRoot;
    }
  }

  private static string? GetReportDirectory(string? reportPath)
  {
    if (reportPath is null)
    {
      return null;
    }

    var slash = reportPath.Replace('\\', '/').LastIndexOf('/');

    return slash > 0 ? reportPath[..slash] : null;
  }

  private static string? ToDirectoryPrefix(string normalizedDirectory) =>
    normalizedDirectory.Length == 0 ? null : normalizedDirectory + "/";

  private static bool IsBareFileName(string path) => !path.Contains('/');

  private static int CountCommonTrailingSegments(string first, string second)
  {
    var left = first.AsSpan();
    var right = second.AsSpan();
    var score = 0;

    while (true)
    {
      var leftSlash = left.LastIndexOf('/');
      var rightSlash = right.LastIndexOf('/');

      if (!left[(leftSlash + 1)..].Equals(right[(rightSlash + 1)..], StringComparison.OrdinalIgnoreCase))
      {
        return score;
      }

      score++;

      if (leftSlash < 0 || rightSlash < 0)
      {
        return score;
      }

      left = left[..leftSlash];
      right = right[..rightSlash];
    }
  }

  // Keeps the entries that pass, unless none does: a preference never empties the list of ties.
  private static List<string> Prefer(List<string> paths, Predicate<string> isPreferred)
  {
    var preferred = paths.FindAll(isPreferred);

    return preferred.Count > 0 ? preferred : paths;
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

  private PathFixResult Retain(string path, Func<string, PathFixResult> toResult)
  {
    var fixedPath = ApplyFixes(path, addPrefixes: true);

    return fixedPath.Length == 0 ? PathFixResult.Unresolved : toResult(fixedPath);
  }

  private PathFixResult Resolve(NetworkIndex network, IReadOnlyList<string> candidates)
  {
    var exact = candidates.FirstOrDefault(network.Contains);

    if (exact is not null)
    {
      return Retain(exact, PathFixResult.Resolved);
    }

    foreach (var candidate in candidates)
    {
      var sameIgnoringCase = network
        .FindByFileName(NetworkIndex.GetFileName(candidate))
        .Where(file => file.Equals(candidate, StringComparison.OrdinalIgnoreCase))
        .ToList();

      if (sameIgnoringCase.Count > 0)
      {
        return BreakTie(sameIgnoringCase);
      }
    }

    return ResolveBySuffix(network, candidates);
  }

  private PathFixResult ResolveBySuffix(NetworkIndex network, IReadOnlyList<string> candidates)
  {
    var bestScore = 0;
    var best = new HashSet<string>(StringComparer.Ordinal);

    foreach (var candidate in candidates)
    {
      foreach (var file in network.FindByFileName(NetworkIndex.GetFileName(candidate)))
      {
        var score = CountCommonTrailingSegments(candidate, file);

        if (score > bestScore)
        {
          bestScore = score;
          best.Clear();
        }

        if (score == bestScore)
        {
          best.Add(file);
        }
      }
    }

    if (bestScore >= MinimumSuffixScore)
    {
      return BreakTie([.. best]);
    }

    var rootFiles = best.Where(IsBareFileName).ToList();

    if (rootFiles.Count > 0)
    {
      return BreakTie(rootFiles);
    }

    return best.Count == 1 && (_context.AllowFilenameOnlyMatch || IsBareFileName(candidates[0]))
      ? Retain(best.First(), PathFixResult.Resolved)
      : PathFixResult.Unresolved;
  }

  private PathFixResult BreakTie(List<string> tied)
  {
    var remaining = Prefer(tied, _context.FlagPaths.Includes);

    if (_reportDirectoryPrefix is { } prefix)
    {
      remaining = Prefer(remaining, file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    var shortestLength = remaining.Min(file => file.Length);
    var shortest = remaining.FindAll(file => file.Length == shortestLength);

    return shortest.Count == 1 ? Retain(shortest[0], PathFixResult.Resolved) : PathFixResult.Ambiguous;
  }

  #endregion
}
