// ------------------------------------------------------------------------------------------------
// <copyright file="IssuedApiTokenResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Tokens;

/// <summary>A personal API token just created, with its value in clear, returned this one time.</summary>
/// <param name="Id">The identifier of the token.</param>
/// <param name="Value">Its value in clear (<c>cvp_…</c>): keep it now, it is never shown again.</param>
/// <param name="Hint">The beginning of the value, kept to recognize it.</param>
/// <param name="Name">The name given to the token.</param>
/// <param name="IsInstanceToken">Whether it belongs to the instance rather than to an organization.</param>
/// <param name="ExpiresAt">When it expires, or <see langword="null" />.</param>
public sealed record IssuedApiTokenResponse(long Id, string Value, string Hint, string Name, bool IsInstanceToken, DateTimeOffset? ExpiresAt)
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the value: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(IssuedApiTokenResponse)} {{ Id = {Id}, Hint = {Hint} }}";

  #endregion
}
