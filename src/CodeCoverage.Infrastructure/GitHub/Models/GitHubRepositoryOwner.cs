// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubRepositoryOwner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The account that holds a repository.</summary>
/// <param name="Login">The login of the account.</param>
internal sealed record GitHubRepositoryOwner(string Login);
