// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenIdentity.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Domain.Contracts.Uploads;

/// <summary>Who an upload token is: what may be remembered between two uploads, and never the decision.</summary>
/// <param name="TokenId">The identifier of the token.</param>
/// <param name="Scope">What the token gives access to.</param>
/// <param name="RepositoryId">The repository of a token of that scope, or <see langword="null" />.</param>
/// <param name="ExpiresAt">When the token expires, or <see langword="null" />.</param>
public sealed record UploadTokenIdentity(long TokenId, UploadTokenScope Scope, long? RepositoryId, DateTimeOffset? ExpiresAt);
