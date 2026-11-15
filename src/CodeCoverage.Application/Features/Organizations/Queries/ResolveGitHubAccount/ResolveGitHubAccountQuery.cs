// ------------------------------------------------------------------------------------------------
// <copyright file="ResolveGitHubAccountQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Application.Features.Organizations.Queries.ResolveGitHubAccount;

/// <summary>Turns the login of a GitHub account into the account, its identifier included, before its admission.</summary>
/// <param name="Login">The login, as the administrator typed it.</param>
public sealed record ResolveGitHubAccountQuery(string Login) : IQuery<GitHubAccountInfo>, IAdminOnly;
