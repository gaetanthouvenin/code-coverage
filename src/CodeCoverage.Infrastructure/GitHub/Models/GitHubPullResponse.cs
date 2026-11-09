// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubPullResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The answer of <c>GET …/pulls/{number}</c>.</summary>
/// <param name="Number">The number of the pull request.</param>
/// <param name="State">The state of the pull request, <c>open</c> or <c>closed</c>.</param>
/// <param name="Title">The title of the pull request.</param>
/// <param name="Merged">A value indicating whether the pull request was merged.</param>
/// <param name="Head">The head of the pull request.</param>
/// <param name="Base">The base of the pull request.</param>
/// <param name="User">The account of the author, or <see langword="null" /> when it no longer exists.</param>
/// <param name="MergeCommitSha">The SHA of the synthetic merge commit, or <see langword="null" /> when GitHub has none.</param>
internal sealed record GitHubPullResponse(
  int Number,
  string State,
  string Title,
  bool Merged,
  GitHubPullBranch Head,
  GitHubPullBranch Base,
  GitHubUser? User = null,
  string? MergeCommitSha = null);
