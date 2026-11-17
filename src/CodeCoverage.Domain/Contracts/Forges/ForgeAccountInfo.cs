// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeAccountInfo.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a forge reports about the account a connection serves.</summary>
/// <param name="ExternalAccountId">The identifier the forge gives the account, which survives a renaming.</param>
/// <param name="Login">
///   The current login of the account, as the forge reports it; it can be empty for an account that has none, such as
///   an enterprise on GitHub.
/// </param>
/// <param name="ServiceAccountExternalId">
///   The identifier the forge gives the account the credential of the connection acts as, or <see langword="null" />
///   when the forge does not say: GitHub, whose App acts as itself, never does.
/// </param>
/// <param name="ExpiresAt">
///   When the credential of the connection expires, or <see langword="null" /> when it does not or is not known.
/// </param>
public sealed record ForgeAccountInfo(
  string ExternalAccountId,
  string Login,
  string? ServiceAccountExternalId = null,
  DateTimeOffset? ExpiresAt = null);
