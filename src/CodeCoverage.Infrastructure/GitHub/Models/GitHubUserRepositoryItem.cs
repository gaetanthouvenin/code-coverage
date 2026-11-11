// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUserRepositoryItem.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>A repository of <c>GET /user/installations/{id}/repositories</c>.</summary>
/// <param name="Id">The numeric identifier of the repository.</param>
/// <param name="Permissions">What the user may do on it, or <see langword="null" /> when GitHub does not say.</param>
internal sealed record GitHubUserRepositoryItem(long Id, GitHubRepositoryPermissions? Permissions = null);
