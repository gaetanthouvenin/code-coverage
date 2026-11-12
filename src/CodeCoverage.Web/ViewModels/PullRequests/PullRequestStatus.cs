// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.PullRequests;

/// <summary>The state of a pull request as the pages show it.</summary>
public enum PullRequestStatus
{
  /// <summary>Open.</summary>
  Open,

  /// <summary>Merged.</summary>
  Merged,

  /// <summary>Closed without being merged.</summary>
  Closed,
}
