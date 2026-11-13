// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Tokens;

/// <summary>A personal token of the API, as its owner sees it.</summary>
/// <param name="Id">The identifier of the token.</param>
/// <param name="Name">The name it was given.</param>
/// <param name="Scope">The login of the organization it belongs to, or <see langword="null" /> for the instance.</param>
/// <param name="Hint">The few characters that recognize it.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="LastUsedAt">When it was last used, or <see langword="null" />.</param>
/// <param name="RevokedAt">When it was revoked, or <see langword="null" /> while it works.</param>
public sealed record ApiTokenViewModel(
  long Id,
  string Name,
  string? Scope,
  string Hint,
  DateTimeOffset CreatedAt,
  DateTimeOffset? LastUsedAt,
  DateTimeOffset? RevokedAt)
{
  #region Properties

  /// <summary>Gets a value indicating whether the token was revoked.</summary>
  public bool IsRevoked => RevokedAt is not null;

  #endregion
}
