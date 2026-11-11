// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubBranchResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>A branch, as <c>GET /repositories/{id}/branches/{branch}</c> answers it.</summary>
/// <param name="Commit">The commit the branch points to, or <see langword="null" />.</param>
internal sealed record GitHubBranchResponse(GitHubBranchCommit? Commit = null);
