// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubBranchCommit.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The commit a branch points to.</summary>
/// <param name="Sha">The SHA of the commit, or <see langword="null" />.</param>
internal sealed record GitHubBranchCommit(string? Sha = null);
