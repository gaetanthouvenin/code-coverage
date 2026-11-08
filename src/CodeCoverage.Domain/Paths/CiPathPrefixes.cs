// ------------------------------------------------------------------------------------------------
// <copyright file="CiPathPrefixes.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace CodeCoverage.Domain.Paths;

/// <summary>The checkout directories of the CI agents, found at the start of absolute paths.</summary>
internal static class CiPathPrefixes
{
  #region Fields

  private const RegexOptions CompileOptions =
    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking;

  private static readonly Regex Workspace = new(
    @"\A(?:home/runner/work/[^/]+/[^/]+/|__w/[^/]+/[^/]+/|a/\d+/s/|a/[^/]+/[^/]+/|home/vsts/work/\d+/s/|agent/_work/\d+/s/)",
    CompileOptions);

  private static readonly Regex UserHome = new(@"\A(?:Users|home)/[^/]+/", CompileOptions);

  #endregion

  #region Methods

  /// <summary>Removes the checkout directory that starts a path.</summary>
  /// <param name="path">The normalized path.</param>
  /// <returns>
  ///   The path without its prefix, or <see langword="null" /> if it has none. A user's home
  ///   directory only counts when no checkout directory of a CI agent matches.
  /// </returns>
  internal static string? Strip(string path)
  {
    var match = Workspace.Match(path);

    if (!match.Success)
    {
      match = UserHome.Match(path);
    }

    return match.Success ? path[match.Length..] : null;
  }

  #endregion
}
