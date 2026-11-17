// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCapabilities.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Gives what GitHub accepts in what this application writes on it.</summary>
internal static class GitHubCapabilities
{
  #region Fields

  /// <summary>The longest body of a comment that is published, in characters; GitHub refuses a comment over 65 536.</summary>
  public const int CommentMaxLength = 60_000;

  #endregion

  #region Properties

  /// <summary>Gets the capabilities of GitHub.</summary>
  public static ForgeCapabilities Value { get; } = new(CommentMaxLength);

  #endregion
}
