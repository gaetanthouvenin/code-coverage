// ------------------------------------------------------------------------------------------------
// <copyright file="IssuedUploadTokenResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Tokens;

/// <summary>An upload token just created, with its value in clear, returned this one time.</summary>
/// <param name="Id">The identifier of the token.</param>
/// <param name="Value">Its value in clear: keep it now, it is never shown again.</param>
/// <param name="Hint">The part of the value kept to recognize it.</param>
/// <param name="Label">The name given to the token.</param>
/// <param name="ExpiresAt">When it expires, or <see langword="null" />.</param>
public sealed record IssuedUploadTokenResponse(long Id, string Value, string Hint, string Label, DateTimeOffset? ExpiresAt)
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the value: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(IssuedUploadTokenResponse)} {{ Id = {Id}, Hint = {Hint} }}";

  #endregion
}
