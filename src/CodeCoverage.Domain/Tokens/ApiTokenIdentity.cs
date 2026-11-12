// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenIdentity.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tokens;

/// <summary>Who a personal token of the API authenticates: its user, and the organization the token is bound to.</summary>
/// <param name="TokenId">The identifier of the token.</param>
/// <param name="UserId">The identifier of the user who owns the token.</param>
/// <param name="Login">The login of the account of the user.</param>
/// <param name="Forge">The forge of the account of the user.</param>
/// <param name="ExternalId">The identifier the forge gives the account of the user.</param>
/// <param name="OrganizationId">The organization of the token, or <see langword="null" /> for a token of the instance.</param>
/// <remarks>A read model: it carries neither the value nor the hash of the token.</remarks>
public sealed record ApiTokenIdentity(long TokenId, long UserId, string Login, Forge Forge, string ExternalId, long? OrganizationId);
