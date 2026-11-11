// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>The <c>GitHub</c> settings group: the GitHub App and the address of its API.</summary>
/// <remarks>
///   Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart, the
///   rotation of the private key included.
/// </remarks>
public sealed class GitHubOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.GitHubGroup;

  #endregion

  #region Properties

  /// <summary>Gets or sets the base URL of the GitHub REST API.</summary>
  /// <value><c>https://api.github.com</c> by default.</value>
  public string ApiBaseUrl { get; set; } = "https://api.github.com";

  /// <summary>Gets or sets the client identifier of the GitHub App, the issuer of its JSON Web Tokens.</summary>
  /// <value>The client identifier, or <see langword="null" /> while the App is not created.</value>
  public string? ClientId { get; set; }

  /// <summary>Gets or sets the slug of the App, which names the account it writes its comments as.</summary>
  /// <value>Empty until the App is created or entered; its comments then cannot be told from those of others.</value>
  public string? AppSlug { get; set; }

  /// <summary>Gets or sets the client secret of the GitHub App, protected: it renews the tokens of the users.</summary>
  public string? ClientSecret { get; set; }

  /// <summary>Gets or sets the protected PEM private key of the GitHub App, never the plain text.</summary>
  /// <value>The protected value, or <see langword="null" /> while the App is not created.</value>
  public string? PrivateKeyPem { get; set; }

  /// <summary>Gets or sets the protected secret that signs the webhooks, never the plain text.</summary>
  /// <value>The protected value, or <see langword="null" /> while the App is not created.</value>
  public string? WebhookSecret { get; set; }

  /// <summary>Gets or sets the protected secret that still signs the webhooks during a rotation.</summary>
  /// <value>The protected value, or <see langword="null" /> outside a rotation.</value>
  public string? WebhookSecretPrevious { get; set; }

  #endregion

  #region Methods

  /// <summary>Gets the base address of the GitHub REST API, to which relative request paths are appended.</summary>
  /// <returns>The absolute address, ending with a slash.</returns>
  /// <exception cref="UriFormatException"><see cref="ApiBaseUrl" /> is not an absolute URL.</exception>
  public Uri GetApiBaseAddress() => new($"{ApiBaseUrl.TrimEnd('/')}/", UriKind.Absolute);

  /// <summary>Decrypts the private key of the GitHub App, at the moment of use.</summary>
  /// <param name="protector">The secret protector.</param>
  /// <returns>The private key in PEM format, which the caller must not keep.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="protector" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The private key is not configured.</exception>
  /// <exception cref="System.Security.Cryptography.CryptographicException">The protected value cannot be decrypted.</exception>
  public string GetPrivateKeyPem(ISecretProtector protector)
  {
    ArgumentNullException.ThrowIfNull(protector);

    InvalidOperationException.ThrowIf(
      string.IsNullOrWhiteSpace(PrivateKeyPem),
      $"The setting '{SettingDefinitions.GitHubPrivateKeyPem}' is not configured."
    );

    return protector.Unprotect(PrivateKeyPem);
  }

  /// <summary>Decrypts the client secret of the GitHub App, at the moment of use.</summary>
  /// <param name="protector">The secret protector.</param>
  /// <returns>The client secret, which the caller must not keep.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="protector" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The client secret is not configured.</exception>
  /// <exception cref="System.Security.Cryptography.CryptographicException">The protected value cannot be decrypted.</exception>
  public string GetClientSecret(ISecretProtector protector)
  {
    ArgumentNullException.ThrowIfNull(protector);

    InvalidOperationException.ThrowIf(
      string.IsNullOrWhiteSpace(ClientSecret),
      $"The setting '{SettingDefinitions.GitHubClientSecret}' is not configured."
    );

    return protector.Unprotect(ClientSecret);
  }

  #endregion
}
