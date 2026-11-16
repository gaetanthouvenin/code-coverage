// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeUser.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>The account of a forge that a user token belongs to.</summary>
/// <param name="ExternalId">The identifier the forge gives the account, as text: it never changes, unlike the login.</param>
/// <param name="Login">The login of the account, which can be renamed.</param>
/// <param name="AvatarUrl">The address of its avatar, or <see langword="null" />.</param>
public sealed record ForgeUser(string ExternalId, string Login, string? AvatarUrl);
