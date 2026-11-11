// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.GitHub;

/// <summary>The GitHub account a user token belongs to.</summary>
/// <param name="ExternalId">The numeric identifier GitHub gives the account, as text: it never changes.</param>
/// <param name="Login">The login of the account, which can be renamed.</param>
/// <param name="AvatarUrl">The address of its avatar, or <see langword="null" />.</param>
public sealed record GitHubUser(string ExternalId, string Login, string? AvatarUrl);
