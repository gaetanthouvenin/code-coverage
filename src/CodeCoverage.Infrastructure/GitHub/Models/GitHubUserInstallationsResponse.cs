// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUserInstallationsResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>One page of <c>GET /user/installations</c>.</summary>
/// <param name="TotalCount">The number of installations over all the pages.</param>
/// <param name="Installations">The installations of the page.</param>
internal sealed record GitHubUserInstallationsResponse(int TotalCount, IReadOnlyList<GitHubInstallationListItem> Installations);
