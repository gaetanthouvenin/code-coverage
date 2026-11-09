// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The answer of <c>GET /repos/{owner}/{repo}/installation</c>.</summary>
/// <param name="Id">The identifier of the installation.</param>
/// <param name="Account">The account the installation belongs to.</param>
internal sealed record GitHubInstallationResponse(long Id, GitHubAccount Account);
