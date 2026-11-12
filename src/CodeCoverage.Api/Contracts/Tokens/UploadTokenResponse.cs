// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Tokens;

/// <summary>An upload token, without its value: only its hash is kept.</summary>
/// <param name="Id">The identifier of the token.</param>
/// <param name="Scope">Whether it serves the organization or one repository.</param>
/// <param name="Label">The name given to the token.</param>
/// <param name="Hint">The part of the value kept to recognize it.</param>
/// <param name="Imported">Whether its value was imported from Codecov.</param>
/// <param name="CreatedBy">Who created it.</param>
/// <param name="CreatedAt">When it was created, in UTC.</param>
/// <param name="ExpiresAt">When it expires, or <see langword="null" />.</param>
/// <param name="LastUsedAt">When it was last used, at most once a minute, or <see langword="null" />.</param>
/// <param name="RevokedAt">When it was revoked, or <see langword="null" />.</param>
/// <param name="RotationAdvisedAt">When a rotation was advised, or <see langword="null" />.</param>
public sealed record UploadTokenResponse(
  long Id,
  string Scope,
  string Label,
  string Hint,
  bool Imported,
  string CreatedBy,
  DateTimeOffset CreatedAt,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset? LastUsedAt,
  DateTimeOffset? RevokedAt,
  DateTimeOffset? RotationAdvisedAt);
