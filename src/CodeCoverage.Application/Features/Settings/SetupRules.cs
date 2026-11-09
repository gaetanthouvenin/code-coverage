// ------------------------------------------------------------------------------------------------
// <copyright file="SetupRules.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings;

/// <summary>The rules shared by the first-start use cases, authenticated by the setup token.</summary>
internal static class SetupRules
{
  #region Fields

  private const string InvalidTokenMessage = "The setup token is missing or invalid.";

  #endregion

  #region Methods

  /// <summary>Refuses a caller who does not hold the setup token.</summary>
  /// <param name="issuer">The issuer of the setup token.</param>
  /// <param name="token">The token, or <see langword="null" />.</param>
  /// <exception cref="UnauthorizedException">The token is not valid (<see cref="SettingErrors.InvalidSetupToken" />).</exception>
  public static void ThrowIfInvalidToken(ISetupTokenIssuer issuer, string? token)
  {
    if (!issuer.IsValid(token))
    {
      throw new UnauthorizedException(SettingErrors.InvalidSetupToken, InvalidTokenMessage);
    }
  }

  /// <summary>Tells whether the GitHub App exists, whatever the source of its identifier.</summary>
  /// <param name="store">The settings store.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns><see langword="true" /> when the App has an identifier.</returns>
  public static async Task<bool> IsAppCreatedAsync(ISettingsStore store, CancellationToken cancellationToken)
  {
    var appId = await store.GetAsync(SettingDefinitions.Find(SettingDefinitions.GitHubAppId)!, cancellationToken).ConfigureAwait(false);

    return !string.IsNullOrEmpty(appId.Value);
  }

  #endregion
}
