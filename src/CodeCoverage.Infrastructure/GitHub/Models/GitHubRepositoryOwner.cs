// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubRepositoryOwner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The account that holds a repository.</summary>
/// <param name="Login">The login of the account.</param>
/// <param name="Id">The numeric identifier of the account, which a rename does not change.</param>
internal sealed record GitHubRepositoryOwner(string Login, long? Id = null);
