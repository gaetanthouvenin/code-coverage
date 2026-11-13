// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryActivityViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.ViewModels.Commits;
using CodeCoverage.Web.ViewModels.PullRequests;

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>What the lower half of the overview shows: the latest commits, the open pull requests and the badge.</summary>
/// <param name="Commits">The newest commits of the default branch.</param>
/// <param name="Pulls">The open pull requests, the newest first.</param>
/// <param name="Badge">The snippets of the badge, or <see langword="null" /> while the public addresses are unknown.</param>
public sealed record RepositoryActivityViewModel(
  IReadOnlyList<CommitRowViewModel> Commits,
  IReadOnlyList<PullRequestRowViewModel> Pulls,
  BadgeSnippetsViewModel? Badge);
