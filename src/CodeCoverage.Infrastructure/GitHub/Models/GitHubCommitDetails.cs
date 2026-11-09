// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCommitDetails.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The git part of a commit.</summary>
/// <param name="Message">The message of the commit.</param>
/// <param name="Committer">The committer, or <see langword="null" /> when GitHub does not know it.</param>
/// <param name="Author">The author, or <see langword="null" /> when GitHub does not know it.</param>
internal sealed record GitHubCommitDetails(
  string Message,
  GitHubGitIdentity? Committer = null,
  GitHubGitIdentity? Author = null);
