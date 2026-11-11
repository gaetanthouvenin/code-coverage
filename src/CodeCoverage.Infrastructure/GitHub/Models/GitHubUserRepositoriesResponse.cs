// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUserRepositoriesResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>One page of <c>GET /user/installations/{id}/repositories</c>.</summary>
/// <param name="TotalCount">The number of repositories over all the pages.</param>
/// <param name="Repositories">The repositories of the page.</param>
internal sealed record GitHubUserRepositoriesResponse(int TotalCount, IReadOnlyList<GitHubUserRepositoryItem> Repositories);
