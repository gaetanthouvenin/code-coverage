// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubPublicAccountResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>A user or an organization, as <c>GET /users/{login}</c> answers it.</summary>
/// <param name="Id">The numeric identifier of the account.</param>
/// <param name="Login">The login of the account.</param>
/// <param name="Type">The kind of the account, <c>User</c> or <c>Organization</c>, or <see langword="null" />.</param>
internal sealed record GitHubPublicAccountResponse(long Id, string Login, string? Type = null);
