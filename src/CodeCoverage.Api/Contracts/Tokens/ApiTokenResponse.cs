// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Tokens;

/// <summary>A personal API token, without its value: only its hash is kept.</summary>
/// <param name="Id">The identifier of the token.</param>
/// <param name="Name">The name given to the token.</param>
/// <param name="Hint">The beginning of the value, kept to recognize it.</param>
/// <param name="IsInstanceToken">Whether it belongs to the instance rather than to an organization.</param>
/// <param name="CreatedAt">When it was created, in UTC.</param>
/// <param name="ExpiresAt">When it expires, or <see langword="null" />.</param>
/// <param name="LastUsedAt">When it was last used, at most once a minute, or <see langword="null" />.</param>
/// <param name="RevokedAt">When it was revoked, or <see langword="null" />.</param>
public sealed record ApiTokenResponse(
  long Id,
  string Name,
  string Hint,
  bool IsInstanceToken,
  DateTimeOffset CreatedAt,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset? LastUsedAt,
  DateTimeOffset? RevokedAt);
