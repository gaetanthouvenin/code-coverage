// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCommitResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>A commit, in the answers of <c>GET …/commits</c> and <c>GET …/commits/{sha}</c>.</summary>
/// <param name="Sha">The SHA of the commit.</param>
/// <param name="Commit">The git part of the commit.</param>
/// <param name="Parents">The parents of the commit, the first parent first.</param>
/// <param name="Author">The account of the author, or <see langword="null" />.</param>
internal sealed record GitHubCommitResponse(
  string Sha,
  GitHubCommitDetails Commit,
  IReadOnlyList<GitHubCommitRef> Parents,
  GitHubAuthor? Author = null);
