// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>The error codes of the GitHub adapters.</summary>
public static class GitHubErrors
{
  #region Fields

  /// <summary>
  ///   GitHub refused a commit status for good: it does not know the repository or the commit, the App may not write
  ///   statuses there, or the commit holds as many statuses as GitHub allows.
  /// </summary>
  public const string StatusRefused = "GitHub.StatusRefused";

  /// <summary>GitHub failed for good: its API answers with an error, or does not answer.</summary>
  public const string Unavailable = "GitHub.Unavailable";

  #endregion
}
