// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubRepositoryPermissions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>What a user may do on a repository, as GitHub reports it for a user token.</summary>
/// <param name="Admin">Whether the user administers the repository.</param>
/// <param name="Push">Whether the user can write to the repository.</param>
/// <param name="Pull">Whether the user can read the repository.</param>
internal sealed record GitHubRepositoryPermissions(bool Admin = false, bool Push = false, bool Pull = false);
