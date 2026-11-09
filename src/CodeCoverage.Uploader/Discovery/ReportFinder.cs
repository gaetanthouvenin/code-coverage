// ------------------------------------------------------------------------------------------------
// <copyright file="ReportFinder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security;

using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Discovery;

/// <summary>Finds the coverage reports to upload (ARCHITECTURE §5.3-1).</summary>
/// <remarks>
///   Nothing here fails a build: a path that cannot be read is a warning and the search goes on.
///   Hidden entries are searched and directory links are not followed, on every platform.
/// </remarks>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="workingDirectory">The absolute directory that relative paths start from.</param>
/// <param name="reporter">Prints the warnings.</param>
internal sealed class ReportFinder(
  Func<string, string?> environment,
  string workingDirectory,
  ConsoleReporter reporter)
{
  #region Fields

  private const string AgentTempDirectoryVariable = "AGENT_TEMPDIRECTORY";

  private static readonly EnumerationOptions ListingOptions = new()
  {
    AttributesToSkip = 0,
    IgnoreInaccessible = false,
  };

  private static readonly GlobMatcher[] DefaultMatchers =
    [.. DefaultPatterns.Reports.Select(pattern => new GlobMatcher(pattern))];

  // The only concession to the platform: on Windows, two spellings of a path are one file.
  private static readonly StringComparer PathComparer =
    OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

  // Sorting raw paths would depend on the platform: \ and / do not sort alike against digits.
  private static readonly Comparer<string> ByForwardSlashPath = Comparer<string>.Create(
    (left, right) => string.CompareOrdinal(
      GlobMatcher.NormalizeSeparators(left),
      GlobMatcher.NormalizeSeparators(right)));

  #endregion

  #region Methods

  /// <summary>Finds the reports.</summary>
  /// <remarks>
  ///   Given files replace the search, whose exclusions do not apply to them. Each one is a path or
  ///   a glob, relative to the working directory; a backslash is always a separator. The result has
  ///   no duplicate and its order is the same on every platform.
  /// </remarks>
  /// <param name="root">The root of the repository.</param>
  /// <param name="files">The paths or globs given by the user; empty to search.</param>
  /// <param name="disableSearch">Whether to search for the reports when no file is given.</param>
  /// <returns>The full paths of the reports; empty, after a warning, when there is none.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public IReadOnlyList<string> Find(
    string root,
    IReadOnlyList<string> files,
    bool disableSearch)
  {
    ArgumentNullException.ThrowIfNull(root);
    ArgumentNullException.ThrowIfNull(files);

    var found = new HashSet<string>(PathComparer);

    if (files.Count > 0)
    {
      foreach (var file in files)
      {
        AddGivenFile(file, found);
      }
    }
    else if (!disableSearch)
    {
      SearchDefaultLocations(root, found);
    }

    if (found.Count == 0)
    {
      reporter.Warning("No coverage report was found.");
    }

    return [.. found.Order(ByForwardSlashPath)];
  }

  private static bool IsAccessFailure(Exception exception)
  {
    return exception is IOException or UnauthorizedAccessException or SecurityException;
  }

  private void SearchDefaultLocations(string root, HashSet<string> found)
  {
    Search(Path.GetFullPath(root, workingDirectory), DefaultMatchers, applyExclusions: true, found);

    var agentTemp = environment(AgentTempDirectoryVariable);

    if (!string.IsNullOrWhiteSpace(agentTemp))
    {
      var directory = Path.GetFullPath(agentTemp, workingDirectory);

      Search(directory, DefaultMatchers, applyExclusions: true, found);
    }
  }

  private void AddGivenFile(string file, HashSet<string> found)
  {
    var normalized = GlobMatcher.NormalizeSeparators(file);
    var path = Path.GetFullPath(normalized, workingDirectory);

    if (File.Exists(path))
    {
      AddIfReadable(path, found);
      return;
    }

    var wildcard = GlobMatcher.IndexOfWildcard(normalized);

    if (wildcard < 0)
    {
      reporter.Warning($"The report '{file}' was not found.");
      return;
    }

    // The directories before the first wildcard are where the walk starts from, absolute or not.
    var patternStart = normalized.LastIndexOf('/', wildcard) + 1;
    var directory = Path.GetFullPath(normalized[..patternStart], workingDirectory);
    var pattern = new GlobMatcher(normalized[patternStart..]);

    if (Search(directory, [pattern], applyExclusions: false, found) == 0)
    {
      reporter.Warning($"No file matches '{file}'.");
    }
  }

  // Walks with an explicit stack: the depth of a tree must not be able to overflow the real one.
  private int Search(
    string directory,
    GlobMatcher[] patterns,
    bool applyExclusions,
    HashSet<string> found)
  {
    var matches = 0;
    var pending = new Stack<(string Path, string RelativePrefix)>();

    pending.Push((directory, string.Empty));

    while (pending.TryPop(out var current))
    {
      foreach (var entry in List(current.Path))
      {
        var relativePath = current.RelativePrefix + entry.Name;

        if (entry is DirectoryInfo)
        {
          if (!entry.Attributes.HasFlag(FileAttributes.ReparsePoint)
              && !(applyExclusions && DefaultPatterns.IsExcludedDirectory(entry.Name)))
          {
            pending.Push((entry.FullName, relativePath + "/"));
          }
        }
        else if (Array.Exists(patterns, pattern => pattern.IsMatch(relativePath))
                 && !(applyExclusions && DefaultPatterns.IsExcludedReport(relativePath)))
        {
          matches++;
          AddIfReadable(entry.FullName, found);
        }
      }
    }

    return matches;
  }

  private List<FileSystemInfo> List(string directory)
  {
    try
    {
      return [.. new DirectoryInfo(directory).EnumerateFileSystemInfos("*", ListingOptions)];
    }
    catch (Exception exception) when (IsAccessFailure(exception))
    {
      WarnUnreadable(directory, exception);

      return [];
    }
  }

  private void AddIfReadable(string path, HashSet<string> found)
  {
    try
    {
      File.OpenHandle(path, share: FileShare.ReadWrite | FileShare.Delete).Dispose();
      found.Add(path);
    }
    catch (Exception exception) when (IsAccessFailure(exception))
    {
      WarnUnreadable(path, exception);
    }
  }

  private void WarnUnreadable(string path, Exception exception)
  {
    reporter.Warning($"'{path}' cannot be read: it is skipped.");
    reporter.Detail(exception.Message);
  }

  #endregion
}
