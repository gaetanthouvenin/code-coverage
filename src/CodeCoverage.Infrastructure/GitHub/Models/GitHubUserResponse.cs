// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUserResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The account of a user token, as <c>GET /user</c> answers it.</summary>
/// <param name="Id">The numeric identifier of the account.</param>
/// <param name="Login">The login of the account.</param>
/// <param name="AvatarUrl">The address of its avatar, or <see langword="null" />.</param>
internal sealed record GitHubUserResponse(long Id, string Login, string? AvatarUrl = null);
