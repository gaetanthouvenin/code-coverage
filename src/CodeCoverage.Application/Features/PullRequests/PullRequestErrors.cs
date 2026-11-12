// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.PullRequests;

/// <summary>The error codes of the pull requests.</summary>
public static class PullRequestErrors
{
  #region Fields

  /// <summary>The repository has no pull request of this number. Maps to HTTP 404 Not Found.</summary>
  public const string NotFound = "PullRequest.NotFound";

  /// <summary>The state names no state of a pull request. Maps to HTTP 400 Bad Request.</summary>
  public const string InvalidState = "PullRequest.InvalidState";

  /// <summary>A page or a page size is out of its bounds. Maps to HTTP 400 Bad Request.</summary>
  public const string InvalidPage = "PullRequest.InvalidPage";

  #endregion
}
