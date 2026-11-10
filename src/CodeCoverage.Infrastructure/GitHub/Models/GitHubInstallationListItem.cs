// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationListItem.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>An installation of the App, as <c>GET /app/installations</c> lists it.</summary>
/// <param name="Id">The identifier of the installation.</param>
/// <param name="Account">The account that installed the App.</param>
/// <param name="SuspendedAt">When the installation was suspended, or <see langword="null" />.</param>
internal sealed record GitHubInstallationListItem(long Id, GitHubInstallationAccount Account, DateTimeOffset? SuspendedAt = null);