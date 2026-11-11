// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationAccount.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The account of an installation, as <c>GET /app/installations</c> lists it.</summary>
/// <param name="Id">The numeric identifier of the account.</param>
/// <param name="Login">The login of the account, or <see langword="null" /> for an enterprise, which has a slug instead.</param>
/// <param name="Type">The kind of the account, <c>User</c> or <c>Organization</c>, or <see langword="null" />.</param>
internal sealed record GitHubInstallationAccount(long Id, string? Login = null, string? Type = null);