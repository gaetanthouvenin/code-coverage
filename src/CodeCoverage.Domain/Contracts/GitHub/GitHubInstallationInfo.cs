// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationInfo.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>An installation of the App, as GitHub lists it.</summary>
/// <param name="Id">The identifier of the installation.</param>
/// <param name="AccountId">The numeric identifier of the account that installed the App, as text.</param>
/// <param name="AccountLogin">The login of that account.</param>
/// <param name="Suspended">Whether the installation is suspended: GitHub then refuses every call made in its name.</param>
public sealed record GitHubInstallationInfo(long Id, string AccountId, string AccountLogin, bool Suspended);
