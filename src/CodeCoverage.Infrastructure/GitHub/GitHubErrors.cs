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

  /// <summary>GitHub failed for good: its API answers with an error, or does not answer.</summary>
  public const string Unavailable = "GitHub.Unavailable";

  #endregion
}
