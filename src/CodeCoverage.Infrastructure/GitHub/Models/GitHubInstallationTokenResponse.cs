// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationTokenResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The answer of <c>POST /app/installations/{id}/access_tokens</c>.</summary>
/// <param name="Token">The installation token.</param>
/// <param name="ExpiresAt">The expiry of the token.</param>
internal sealed record GitHubInstallationTokenResponse(string Token, DateTimeOffset ExpiresAt);
