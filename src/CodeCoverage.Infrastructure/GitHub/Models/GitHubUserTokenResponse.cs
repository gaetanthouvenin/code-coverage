// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUserTokenResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The answer of <c>POST /login/oauth/access_token</c>, which is HTTP 200 for a refusal as well.</summary>
/// <param name="AccessToken">The new user token, or <see langword="null" /> for a refusal.</param>
/// <param name="ExpiresIn">The lifetime of the user token in seconds, or <see langword="null" /> when it does not expire.</param>
/// <param name="RefreshToken">The new refresh token, or <see langword="null" />.</param>
/// <param name="RefreshTokenExpiresIn">The lifetime of the refresh token in seconds, or <see langword="null" />.</param>
/// <param name="Error">The code of the refusal, such as <c>bad_refresh_token</c>, or <see langword="null" />.</param>
internal sealed record GitHubUserTokenResponse(
  string? AccessToken = null,
  int? ExpiresIn = null,
  string? RefreshToken = null,
  int? RefreshTokenExpiresIn = null,
  string? Error = null)
{
  #region Methods

  /// <summary>Describes the answer without its tokens, which must never reach a log.</summary>
  /// <returns>The lifetimes and the error, each token replaced by whether it is present.</returns>
  public override string ToString() =>
    string.Create(
      CultureInfo.InvariantCulture,
      $"{nameof(GitHubUserTokenResponse)} {{ AccessToken = {Mask(AccessToken)}, ExpiresIn = {ExpiresIn}, RefreshToken = {Mask(RefreshToken)}, RefreshTokenExpiresIn = {RefreshTokenExpiresIn}, Error = {Error} }}"
    );

  /// <summary>Masks a token.</summary>
  /// <param name="token">The token, or <see langword="null" />.</param>
  /// <returns><c>***</c> for a token, an empty text otherwise.</returns>
  private static string Mask(string? token) => token is null ? string.Empty : "***";

  #endregion
}
