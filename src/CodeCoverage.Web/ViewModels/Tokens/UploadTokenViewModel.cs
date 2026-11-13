// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Tokens;

/// <summary>A token of a list: what recognizes it, never anything that would let it be used again.</summary>
/// <param name="Id">The identifier of the token.</param>
/// <param name="Label">The name it was given.</param>
/// <param name="Hint">The few characters that recognize it: the start of a generated value, the end of an imported one.</param>
/// <param name="Imported">Whether its value comes from Codecov.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="LastUsedAt">When it was last used, or <see langword="null" />.</param>
/// <param name="RevokedAt">When it was revoked, or <see langword="null" /> while it works.</param>
public sealed record UploadTokenViewModel(
  long Id,
  string Label,
  string Hint,
  bool Imported,
  DateTimeOffset CreatedAt,
  DateTimeOffset? LastUsedAt,
  DateTimeOffset? RevokedAt)
{
  #region Properties

  /// <summary>Gets a value indicating whether the token was revoked.</summary>
  public bool IsRevoked => RevokedAt is not null;

  #endregion
}
