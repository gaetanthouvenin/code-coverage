// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAccountInfo.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>A GitHub account, a user or an organization, as <c>GET /users/{login}</c> describes it.</summary>
/// <param name="ExternalId">The numeric identifier GitHub gives the account, as text: it never changes.</param>
/// <param name="Login">The login of the account, as GitHub spells it.</param>
/// <param name="AccountType">Whether the account is a user or an organization.</param>
public sealed record GitHubAccountInfo(string ExternalId, string Login, OrganizationAccountType AccountType);
