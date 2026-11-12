// ------------------------------------------------------------------------------------------------
// <copyright file="CreateApiTokenRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Tokens;

/// <summary>Asks for a personal API token, bound to one organization or to the instance.</summary>
/// <param name="Name">The name given to the token.</param>
/// <param name="Forge">The forge segment of the organization, e.g. <c>github</c>, or <see langword="null" /> for a token of the instance.</param>
/// <param name="Organization">The login of the organization, or <see langword="null" /> for a token of the instance.</param>
/// <param name="ExpiresAt">When it expires, or <see langword="null" />.</param>
public sealed record CreateApiTokenRequest(string Name, string? Forge, string? Organization, DateTimeOffset? ExpiresAt);
