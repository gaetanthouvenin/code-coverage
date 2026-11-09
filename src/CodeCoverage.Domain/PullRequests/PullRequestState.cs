// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.PullRequests;

/// <summary>The state of a pull request.</summary>
public enum PullRequestState
{
  /// <summary>The pull request is open.</summary>
  Open,

  /// <summary>The pull request was closed without being merged.</summary>
  Closed,

  /// <summary>The pull request was merged.</summary>
  Merged,
}
