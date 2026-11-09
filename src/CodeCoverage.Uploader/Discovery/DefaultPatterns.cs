// ------------------------------------------------------------------------------------------------
// <copyright file="DefaultPatterns.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Discovery;

/// <summary>The globs and the exclusions of the search for reports (ARCHITECTURE §5.3).</summary>
/// <remarks>
///   The globs name only the formats the server parses, where the Codecov CLI casts a far wider net
///   and then needs over a hundred exclusions on the file name to narrow it. Directory names are
///   compared with their case, on every platform, as in the Codecov CLI.
/// </remarks>
internal static class DefaultPatterns
{
  #region Fields

  private const string TestResultsDirectory = "TestResults";

  private static readonly string[] ExcludedDirectories =
    ["node_modules", ".git", "vendor", ".venv"];

  private static readonly string[] BuildOutputDirectories = ["bin", "obj"];

  #endregion

  #region Properties

  /// <summary>Gets the globs that select a report.</summary>
  public static IReadOnlyList<string> Reports { get; } =
  [
    "**/coverage.cobertura.xml",
    "**/*cobertura*.xml",
    "**/coverage.opencover.xml",
    "**/lcov.info",
    "**/*.lcov",
    "**/jacoco*.xml",
    "**/clover.xml",
  ];

  #endregion

  #region Methods

  /// <summary>Tells whether a directory is left out of the search, with all its content.</summary>
  /// <param name="name">The name of the directory.</param>
  /// <returns><see langword="true" /> when the directory is not searched.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static bool IsExcludedDirectory(string name)
  {
    ArgumentNullException.ThrowIfNull(name);

    return ExcludedDirectories.Contains(name, StringComparer.Ordinal);
  }

  /// <summary>Tells whether a report lies in a build output, outside of any test results.</summary>
  /// <remarks>
  ///   The build outputs are walked rather than pruned: <c>dotnet test</c> may well write
  ///   <c>TestResults</c> inside <c>bin</c>. Only the path below the searched directory counts.
  /// </remarks>
  /// <param name="relativePath">The path of the report below the searched directory.</param>
  /// <returns><see langword="true" /> when the report must be left out.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static bool IsExcludedReport(string relativePath)
  {
    ArgumentNullException.ThrowIfNull(relativePath);

    var directories = relativePath.Split('/')[..^1];

    return directories.Intersect(BuildOutputDirectories, StringComparer.Ordinal).Any()
           && !directories.Contains(TestResultsDirectory, StringComparer.Ordinal);
  }

  #endregion
}
