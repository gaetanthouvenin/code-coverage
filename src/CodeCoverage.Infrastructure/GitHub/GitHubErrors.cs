// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>The error codes proper to the GitHub adapters.</summary>
/// <remarks>
///   A refused status and an unavailable forge carry the codes shared by every forge, those of
///   <see cref="Forges.ForgeErrors" />.
/// </remarks>
public static class GitHubErrors
{
  #region Fields

  /// <summary>
  ///   GitHub refused a request sent while an HTTP request was handled, because the instance reached one of its rate
  ///   limits: the caller may try again once it is reset.
  /// </summary>
  public const string RateLimited = "GitHub.RateLimited";

  #endregion
}
