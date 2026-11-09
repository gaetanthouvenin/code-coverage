// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubTreeResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The answer of <c>GET …/git/trees/{sha}</c>.</summary>
/// <param name="Tree">The entries of the tree.</param>
/// <param name="Truncated">A value indicating whether GitHub left out some of the entries.</param>
internal sealed record GitHubTreeResponse(IReadOnlyList<GitHubTreeEntry> Tree, bool Truncated);
