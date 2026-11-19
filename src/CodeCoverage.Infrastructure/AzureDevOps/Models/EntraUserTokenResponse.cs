// ------------------------------------------------------------------------------------------------
// <copyright file="EntraUserTokenResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json.Serialization;

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>The answer of the Microsoft Entra ID token endpoint to the renewal of a user token, a refusal included.</summary>
/// <param name="AccessToken">The new user token, or <see langword="null" /> for a refusal.</param>
/// <param name="ExpiresIn">The lifetime of the user token in seconds, or <see langword="null" />.</param>
/// <param name="RefreshToken">The new refresh token, or <see langword="null" /> when Entra ID keeps the one it was sent.</param>
/// <param name="Error">The code of the refusal, such as <c>invalid_grant</c>, or <see langword="null" />.</param>
/// <remarks>The endpoint names its fields in <c>snake_case</c>, unlike the Azure DevOps API.</remarks>
internal sealed record EntraUserTokenResponse(
  [property: JsonPropertyName("access_token")] string? AccessToken = null,
  [property: JsonPropertyName("expires_in")] long? ExpiresIn = null,
  [property: JsonPropertyName("refresh_token")] string? RefreshToken = null,
  [property: JsonPropertyName("error")] string? Error = null)
{
  #region Methods

  /// <summary>Describes the answer without its tokens, which must never reach a log.</summary>
  /// <returns>The lifetime and the error, each token replaced by whether it is present.</returns>
  public override string ToString() =>
    string.Create(
      CultureInfo.InvariantCulture,
      $"{nameof(EntraUserTokenResponse)} {{ AccessToken = {Mask(AccessToken)}, ExpiresIn = {ExpiresIn}, RefreshToken = {Mask(RefreshToken)}, Error = {Error} }}"
    );

  /// <summary>Masks a token.</summary>
  /// <param name="token">The token, or <see langword="null" />.</param>
  /// <returns><c>***</c> for a token, an empty text otherwise.</returns>
  private static string Mask(string? token) => token is null ? string.Empty : "***";

  #endregion
}
