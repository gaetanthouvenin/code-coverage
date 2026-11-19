// ------------------------------------------------------------------------------------------------
// <copyright file="EntraClientAuthentication.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography.X509Certificates;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>
///   Authenticates the Microsoft Entra ID application of the instance in a request to a token endpoint: with its
///   certificate when <c>AzureDevOps:CertificatePem</c> is filled, else with its client secret.
/// </summary>
/// <remarks>
///   Shared by the token of the application (<see cref="AzureDevOpsTokenProvider" />) and the renewal of the token of a
///   user (<see cref="AzureDevOpsUserTokenProvider" />): both are requests of the same confidential client. The secret
///   and the certificate are decrypted at the moment of use and never kept.
/// </remarks>
internal static class EntraClientAuthentication
{
  #region Fields

  private const string ClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

  /// <summary>The time a client assertion stays valid.</summary>
  private static readonly TimeSpan AssertionLifetime = TimeSpan.FromMinutes(10);

  private static readonly JsonWebTokenHandler JwtHandler = new() { SetDefaultTimesOnTokenCreation = false };

  #endregion

  #region Methods

  /// <summary>Adds the fields that authenticate the application to the form of a token request.</summary>
  /// <param name="form">The fields of the form.</param>
  /// <param name="settings">The <c>AzureDevOps</c> settings.</param>
  /// <param name="tokenEndpoint">The token endpoint, the audience of a client assertion.</param>
  /// <param name="protector">The secret protector.</param>
  /// <param name="now">The current time, from which a client assertion is valid.</param>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="form" />, <paramref name="settings" />, <paramref name="tokenEndpoint" /> or
  ///   <paramref name="protector" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="InvalidOperationException">The client identifier, or both the secret and the certificate, are not configured.</exception>
  public static void AddTo(
    IDictionary<string, string> form,
    AzureDevOpsOptions settings,
    Uri tokenEndpoint,
    ISecretProtector protector,
    DateTimeOffset now)
  {
    ArgumentNullException.ThrowIfNull(form);
    ArgumentNullException.ThrowIfNull(settings);
    ArgumentNullException.ThrowIfNull(tokenEndpoint);
    ArgumentNullException.ThrowIfNull(protector);

    InvalidOperationException.ThrowIf(
      string.IsNullOrWhiteSpace(settings.ClientId),
      $"The setting '{SettingDefinitions.AzureDevOpsClientId}' is not configured."
    );

    form["client_id"] = settings.ClientId;

    if (string.IsNullOrWhiteSpace(settings.CertificatePem))
    {
      form["client_secret"] = settings.GetClientSecret(protector);
    }
    else
    {
      form["client_assertion_type"] = ClientAssertionType;
      form["client_assertion"] = CreateClientAssertion(settings, tokenEndpoint, protector, now);
    }
  }

  /// <summary>Creates the client assertion that authenticates the application with its certificate.</summary>
  /// <param name="settings">The <c>AzureDevOps</c> settings.</param>
  /// <param name="tokenEndpoint">The token endpoint, the audience of the assertion.</param>
  /// <param name="protector">The secret protector.</param>
  /// <param name="now">The current time.</param>
  /// <returns>The assertion, signed in RS256 with the private key of the certificate.</returns>
  private static string CreateClientAssertion(AzureDevOpsOptions settings, Uri tokenEndpoint, ISecretProtector protector, DateTimeOffset now)
  {
    var pem = settings.GetCertificatePem(protector);

    using var certificate = X509Certificate2.CreateFromPem(pem, pem);

    return JwtHandler.CreateToken(
      new SecurityTokenDescriptor
      {
        Issuer = settings.ClientId,
        Audience = tokenEndpoint.AbsoluteUri,
        Claims = new Dictionary<string, object>
        {
          [JwtRegisteredClaimNames.Sub] = settings.ClientId!,
          [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
        },
        IssuedAt = now.UtcDateTime,
        NotBefore = now.UtcDateTime,
        Expires = (now + AssertionLifetime).UtcDateTime,
        // The certificate dies with this method: a cached signature provider would outlive it.
        SigningCredentials = new X509SigningCredentials(certificate, SecurityAlgorithms.RsaSha256)
        {
          CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
        },
      }
    );
  }

  #endregion
}
