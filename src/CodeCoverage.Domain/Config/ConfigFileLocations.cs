// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigFileLocations.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>Where the configuration file of a repository is looked for, as Codecov does.</summary>
public static class ConfigFileLocations
{
  #region Properties

  /// <summary>Gets the names the file can have, in the order they are tried.</summary>
  public static IReadOnlyList<string> FileNames { get; } = ["codecov.yml", ".codecov.yml", "codecov.yaml", ".codecov.yaml"];

  /// <summary>Gets the directories searched after the root, in the order they are tried.</summary>
  public static IReadOnlyList<string> Directories { get; } = [".github", "dev"];

  #endregion

  #region Methods

  /// <summary>Tells whether a path of a repository is one where the configuration file is looked for.</summary>
  /// <param name="path">The path from the root of the repository, with forward slashes; possibly <see langword="null" />.</param>
  /// <returns>
  ///   <see langword="true" /> for one of <see cref="FileNames" /> at the root or in one of
  ///   <see cref="Directories" />; the comparison is case-sensitive, as paths are in git.
  /// </returns>
  public static bool IsConfigPath(string? path)
  {
    if (string.IsNullOrEmpty(path))
    {
      return false;
    }

    var separator = path.LastIndexOf('/');
    var name = path[(separator + 1)..];

    return FileNames.Contains(name, StringComparer.Ordinal)
           && (separator < 0 || Directories.Contains(path[..separator], StringComparer.Ordinal));
  }

  #endregion
}
