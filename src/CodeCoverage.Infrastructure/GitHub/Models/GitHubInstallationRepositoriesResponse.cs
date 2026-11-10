// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationRepositoriesResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>A page of <c>GET /installation/repositories</c>.</summary>
/// <param name="TotalCount">How many repositories the installation serves, over every page.</param>
/// <param name="Repositories">The repositories of the page.</param>
internal sealed record GitHubInstallationRepositoriesResponse(int TotalCount, IReadOnlyList<GitHubRepositoryResponse> Repositories);