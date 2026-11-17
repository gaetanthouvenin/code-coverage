// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>
///   The <c>AzureDevOps</c> settings group: the multi-tenant Microsoft Entra ID application of the instance and the
///   addresses of its authority and of the Azure DevOps API (ADR-014).
/// </summary>
/// <remarks>
///   Read through <c>IOptionsMonitor&lt;T&gt;</c>: a change in the settings store applies without a restart, the
///   rotation of the client secret or of the certificate included.
/// </remarks>
public sealed class AzureDevOpsOptions
{
  #region Fields

  /// <summary>The name of the configuration section.</summary>
  public const string SectionName = SettingDefinitions.AzureDevOpsGroup;

  #endregion

  #region Properties

  /// <summary>Gets or sets the base URL of the Azure DevOps REST API.</summary>
  /// <value><c>https://dev.azure.com</c> by default.</value>
  public string ApiBaseUrl { get; set; } = "https://dev.azure.com";

  /// <summary>Gets or sets the address of the Microsoft Entra ID authority, to which the tenant is appended.</summary>
  /// <value><c>https://login.microsoftonline.com</c> by default.</value>
  public string Authority { get; set; } = "https://login.microsoftonline.com";

  /// <summary>Gets or sets the protected certificate of the application, in PEM format with its private key.</summary>
  /// <value>The protected value, or <see langword="null" /> when the application authenticates with its client secret.</value>
  public string? CertificatePem { get; set; }

  /// <summary>Gets or sets the client identifier of the Microsoft Entra ID application.</summary>
  /// <value>The identifier, or <see langword="null" /> while the application is not registered; organizations are then not admitted.</value>
  public string? ClientId { get; set; }

  /// <summary>Gets or sets the protected client secret of the application, never the plain text.</summary>
  /// <value>The protected value, or <see langword="null" /> when the application authenticates with its certificate.</value>
  public string? ClientSecret { get; set; }

  #endregion

  #region Methods

  /// <summary>Gets the base address of the Azure DevOps REST API, to which relative request paths are appended.</summary>
  /// <returns>The absolute address, ending with a slash.</returns>
  /// <exception cref="UriFormatException"><see cref="ApiBaseUrl" /> is not an absolute URL.</exception>
  public Uri GetApiBaseAddress() => new($"{ApiBaseUrl.TrimEnd('/')}/", UriKind.Absolute);

  /// <summary>Gets the token endpoint of a tenant, where the application asks for its tokens.</summary>
  /// <param name="tenantId">The directory tenant of the connection.</param>
  /// <returns>The absolute address of the OAuth 2.0 v2 token endpoint of the tenant.</returns>
  /// <exception cref="UriFormatException"><see cref="Authority" /> is not an absolute URL.</exception>
  public Uri GetTokenEndpoint(Guid tenantId) =>
    new(string.Create(CultureInfo.InvariantCulture, $"{Authority.TrimEnd('/')}/{tenantId:D}/oauth2/v2.0/token"), UriKind.Absolute);

  /// <summary>Tells whether the application can authenticate: its client identifier and a secret or a certificate are filled.</summary>
  /// <returns><see langword="true" /> when a token can be asked for.</returns>
  public bool HasCredential() =>
    !string.IsNullOrWhiteSpace(ClientId) && (!string.IsNullOrWhiteSpace(ClientSecret) || !string.IsNullOrWhiteSpace(CertificatePem));

  /// <summary>Decrypts the client secret of the application, at the moment of use.</summary>
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
      $"The setting '{SettingDefinitions.AzureDevOpsClientSecret}' is not configured."
    );

    return protector.Unprotect(ClientSecret);
  }

  /// <summary>Decrypts the certificate of the application, at the moment of use.</summary>
  /// <param name="protector">The secret protector.</param>
  /// <returns>The certificate and its private key in PEM format, which the caller must not keep.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="protector" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The certificate is not configured.</exception>
  /// <exception cref="System.Security.Cryptography.CryptographicException">The protected value cannot be decrypted.</exception>
  public string GetCertificatePem(ISecretProtector protector)
  {
    ArgumentNullException.ThrowIfNull(protector);

    InvalidOperationException.ThrowIf(
      string.IsNullOrWhiteSpace(CertificatePem),
      $"The setting '{SettingDefinitions.AzureDevOpsCertificatePem}' is not configured."
    );

    return protector.Unprotect(CertificatePem);
  }

  #endregion
}
