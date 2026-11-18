// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsTokenProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Polly;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.AzureDevOps.Models;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>
///   Gets the token of the Microsoft Entra ID application of the instance for an Azure DevOps connection, by the client
///   credentials flow on the tenant of the connection.
/// </summary>
/// <param name="credentials">The reader of the connections, which refuses one of another organization or a suspended one.</param>
/// <param name="httpClientFactory">The factory of the client of Microsoft Entra ID.</param>
/// <param name="options">The <c>AzureDevOps</c> settings, read at each call so that a new secret applies at once.</param>
/// <param name="protector">The secret protector, which decrypts the secret or the certificate at the moment of use.</param>
/// <param name="cache">The memory cache of the process, the only place where a token is kept.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     A token is cached under a key <c>{tenant}:{connection}</c>, next to a fingerprint of the settings of the
///     application: a new client identifier, secret, certificate or authority makes the cached token stale at once,
///     without a restart. The token is never persisted nor logged.
///   </para>
///   <para>
///     The application authenticates with its certificate when <c>AzureDevOps:CertificatePem</c> is filled, else with
///     its client secret. Two first requests at the same moment may both ask Entra ID for a token: a second client
///     credentials token is harmless.
///   </para>
/// </remarks>
internal sealed class AzureDevOpsTokenProvider(
  ForgeConnectionCredentialReader credentials,
  IHttpClientFactory httpClientFactory,
  IOptionsMonitor<AzureDevOpsOptions> options,
  ISecretProtector protector,
  IMemoryCache cache,
  IClock clock)
{
  #region Fields

  /// <summary>The scope of the Azure DevOps resource, for an application token.</summary>
  public const string Scope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

  private const string ClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

  /// <summary>How long before its expiry a token stops being served from the cache.</summary>
  private static readonly TimeSpan RenewalMargin = TimeSpan.FromMinutes(5);

  /// <summary>The time a client assertion stays valid.</summary>
  private static readonly TimeSpan AssertionLifetime = TimeSpan.FromMinutes(10);

  private static readonly JsonWebTokenHandler JwtHandler = new() { SetDefaultTimesOnTokenCreation = false };

  #endregion

  #region Methods

  /// <summary>Gets the token of the application for a connection, from the memory cache when it is not about to expire.</summary>
  /// <param name="connectionId">The identifier of the Azure DevOps connection, of the organization of the scope.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The access token, which is never persisted.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connectionId" /> is <see langword="null" />.</exception>
  /// <exception cref="BadGatewayException">
  ///   The connection cannot be used by the organization of the scope, is not an Azure DevOps connection with a tenant,
  ///   or Microsoft Entra ID refused the token or answered with an unreadable payload (<see cref="ForgeErrors.Unavailable" />).
  /// </exception>
  /// <exception cref="InvalidOperationException">The client identifier, or both the secret and the certificate, are not configured.</exception>
  public async Task<string> GetTokenAsync(string connectionId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(connectionId);

    var connection = await credentials.ReadAsync(connectionId, cancellationToken).ConfigureAwait(false);

    return await GetTokenAsync(connection, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Gets the token of the application for a connection already read, from the memory cache when it is not about to expire.</summary>
  /// <param name="connection">The connection, read by <see cref="ForgeConnectionCredentialReader" /> in this scope.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The access token, which is never persisted.</returns>
  /// <remarks>A gateway that needs the connection as well reads it once, then asks for its token here.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="connection" /> is <see langword="null" />.</exception>
  /// <exception cref="BadGatewayException">
  ///   The connection is not an Azure DevOps connection with a tenant, or Microsoft Entra ID refused the token or
  ///   answered with an unreadable payload (<see cref="ForgeErrors.Unavailable" />).
  /// </exception>
  /// <exception cref="InvalidOperationException">The client identifier, or both the secret and the certificate, are not configured.</exception>
  public async Task<string> GetTokenAsync(ForgeConnectionCredential connection, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(connection);

    BadGatewayException.ThrowIfConnectionUnusable(connection.Forge is not Forge.AzureDevOps || connection.TenantId is null, connection.ConnectionId);

    var tenantId = connection.TenantId.Value;
    var settings = options.CurrentValue;
    var fingerprint = Fingerprint(settings);
    var key = string.Create(CultureInfo.InvariantCulture, $"azure-devops:token:{tenantId:D}:{connection.ConnectionId}");

    if (cache.TryGetValue<(string Fingerprint, string Token)>(key, out var cached)
        && string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
    {
      return cached.Token;
    }

    var issued = await RequestTokenAsync(settings, tenantId, cancellationToken).ConfigureAwait(false);
    var lifetime = TimeSpan.FromSeconds(issued.ExpiresIn) - RenewalMargin;

    if (lifetime > TimeSpan.Zero)
    {
      cache.Set(key, (fingerprint, issued.AccessToken), lifetime);
    }

    return issued.AccessToken;
  }

  /// <summary>Computes a fingerprint of the settings that authenticate the application, from their protected values.</summary>
  /// <param name="settings">The <c>AzureDevOps</c> settings.</param>
  /// <returns>The SHA-256 of the settings, in hexadecimal; no secret can be read back from it.</returns>
  private static string Fingerprint(AzureDevOpsOptions settings)
  {
    var text = string.Join('\n', settings.Authority, settings.ClientId, settings.ClientSecret, settings.CertificatePem);

    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
  }

  /// <summary>Sends a token request through the resilience pipeline of the client of Microsoft Entra ID.</summary>
  /// <param name="client">The client of Microsoft Entra ID.</param>
  /// <param name="tokenEndpoint">The token endpoint.</param>
  /// <param name="content">The form of the request.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The response of the last attempt.</returns>
  /// <exception cref="BadGatewayException">The network failed after the retries, a timeout expired or the circuit is open.</exception>
  private static async Task<HttpResponseMessage> PostAsync(
    HttpClient client,
    Uri tokenEndpoint,
    HttpContent content,
    CancellationToken cancellationToken)
  {
    try
    {
      return await client.PostAsync(tokenEndpoint, content, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is HttpRequestException or ExecutionRejectedException)
    {
      throw BadGatewayException.ForgeUnavailable("Microsoft Entra ID does not answer.", exception);
    }
  }

  /// <summary>Creates the client assertion that authenticates the application with its certificate.</summary>
  /// <param name="settings">The <c>AzureDevOps</c> settings.</param>
  /// <param name="tokenEndpoint">The token endpoint, the audience of the assertion.</param>
  /// <returns>The assertion, signed in RS256 with the private key of the certificate.</returns>
  private string CreateClientAssertion(AzureDevOpsOptions settings, Uri tokenEndpoint)
  {
    var pem = settings.GetCertificatePem(protector);

    using var certificate = X509Certificate2.CreateFromPem(pem, pem);

    var now = clock.UtcNow;

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

  /// <summary>Builds the form of a client credentials request.</summary>
  /// <param name="settings">The <c>AzureDevOps</c> settings.</param>
  /// <param name="tokenEndpoint">The token endpoint.</param>
  /// <returns>The fields of the form.</returns>
  /// <exception cref="InvalidOperationException">The client identifier, or both the secret and the certificate, are not configured.</exception>
  private Dictionary<string, string> CreateForm(AzureDevOpsOptions settings, Uri tokenEndpoint)
  {
    InvalidOperationException.ThrowIf(
      string.IsNullOrWhiteSpace(settings.ClientId),
      $"The setting '{SettingDefinitions.AzureDevOpsClientId}' is not configured."
    );

    var form = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["client_id"] = settings.ClientId,
      ["scope"] = Scope,
      ["grant_type"] = "client_credentials",
    };

    if (string.IsNullOrWhiteSpace(settings.CertificatePem))
    {
      form["client_secret"] = settings.GetClientSecret(protector);
    }
    else
    {
      form["client_assertion_type"] = ClientAssertionType;
      form["client_assertion"] = CreateClientAssertion(settings, tokenEndpoint);
    }

    return form;
  }

  /// <summary>Asks Microsoft Entra ID for a new token of the application on a tenant.</summary>
  /// <param name="settings">The <c>AzureDevOps</c> settings.</param>
  /// <param name="tenantId">The tenant of the connection.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The issued token and its lifetime.</returns>
  /// <exception cref="BadGatewayException">Entra ID refused to issue the token or answered with an unreadable payload.</exception>
  private async Task<EntraTokenResponse> RequestTokenAsync(AzureDevOpsOptions settings, Guid tenantId, CancellationToken cancellationToken)
  {
    var tokenEndpoint = settings.GetTokenEndpoint(tenantId);

    using var client = httpClientFactory.CreateClient(AzureDevOpsHttpClientConfiguration.EntraClientName);
    using var content = new FormUrlEncodedContent(CreateForm(settings, tokenEndpoint));
    using var response = await PostAsync(client, tokenEndpoint, content, cancellationToken).ConfigureAwait(false);

    BadGatewayException.ThrowIfForgeRefused(
      !response.IsSuccessStatusCode,
      string.Create(CultureInfo.InvariantCulture, $"Microsoft Entra ID refused the token of tenant {tenantId:D}: HTTP {(int)response.StatusCode}.")
    );

    try
    {
      return await response.Content
                           .ReadFromJsonAsync(AzureDevOpsJsonContext.Default.EntraTokenResponse, cancellationToken)
                           .ConfigureAwait(false)
             ?? throw new JsonException("The payload is empty.");
    }
    catch (JsonException exception)
    {
      throw BadGatewayException.ForgeUnavailable(
        string.Create(CultureInfo.InvariantCulture, $"Microsoft Entra ID answered with an unreadable token for tenant {tenantId:D}."),
        exception
      );
    }
  }

  #endregion
}
