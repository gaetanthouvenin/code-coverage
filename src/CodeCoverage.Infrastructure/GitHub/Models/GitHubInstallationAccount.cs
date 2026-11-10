// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationAccount.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The account of an installation, as <c>GET /app/installations</c> lists it.</summary>
/// <param name="Id">The numeric identifier of the account.</param>
/// <param name="Login">The login of the account.</param>
internal sealed record GitHubInstallationAccount(long Id, string Login);