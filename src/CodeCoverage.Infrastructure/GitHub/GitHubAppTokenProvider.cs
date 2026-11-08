// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppTokenProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.GitHub.Models;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Authenticates the server against GitHub: the JSON Web Token of the App and the installation tokens.</summary>
/// <param name="httpClientFactory">The factory of the <c>github</c> client.</param>
/// <param name="options">The GitHub settings, read at each use so that a new private key applies at once.</param>
/// <param name="protector">The secret protector, which decrypts the private key at the moment of use.</param>
/// <param name="cache">The memory cache of the process, the only place where an installation token is kept.</param>
/// <param name="clock">The clock.</param>
internal sealed class GitHubAppTokenProvider(
  IHttpClientFactory httpClientFactory,
  IOptionsMonitor<GitHubOptions> options,
  ISecretProtector protector,
  IMemoryCache cache,
  IClock clock)
{
  #region Fields

  /// <summary>How far back the issue time of a JSON Web Token is set, to absorb a clock drift with GitHub.</summary>
  private static readonly TimeSpan JwtBackdating = TimeSpan.FromSeconds(60);

  /// <summary>The time a JSON Web Token stays valid, under the 10 minutes allowed by GitHub.</summary>
  private static readonly TimeSpan JwtLifetime = TimeSpan.FromMinutes(9);

  /// <summary>How long before its expiry an installation token stops being served from the cache.</summary>
  private static readonly TimeSpan RenewalMargin = TimeSpan.FromMinutes(10);

  private static readonly JsonWebTokenHandler JwtHandler = new() { SetDefaultTimesOnTokenCreation = false };

  private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

  #endregion

  #region Methods

  /// <summary>Creates a JSON Web Token that authenticates the GitHub App itself.</summary>
  /// <returns>The token, signed in RS256 with the current private key.</returns>
  /// <exception cref="InvalidOperationException">The client identifier or the private key is not configured.</exception>
  public string CreateAppJwt()
  {
    var settings = options.CurrentValue;

    InvalidOperationException.ThrowIf(
      string.IsNullOrWhiteSpace(settings.ClientId),
      $"The setting '{SettingDefinitions.GitHubClientId}' is not configured."
    );

    using var rsa = RSA.Create();
    rsa.ImportFromPem(settings.GetPrivateKeyPem(protector));

    var now = clock.UtcNow;

    return JwtHandler.CreateToken(
      new SecurityTokenDescriptor
      {
        Issuer = settings.ClientId,
        IssuedAt = (now - JwtBackdating).UtcDateTime,
        Expires = (now + JwtLifetime).UtcDateTime,
        SigningCredentials = new SigningCredentials(
          // The key dies with this method: a cached signature provider would outlive it.
          new RsaSecurityKey(rsa) { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } },
          SecurityAlgorithms.RsaSha256
        ),
      }
    );
  }

  /// <summary>Gets the token of an installation, from the memory cache when it is not about to expire.</summary>
  /// <param name="installationId">The identifier of the GitHub App installation.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The installation token, which is never persisted.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="installationId" /> is zero or negative.</exception>
  /// <exception cref="BadGatewayException">GitHub refused to issue the token or answered with an unreadable payload.</exception>
  /// <exception cref="GitHubRateLimitedException">GitHub rate limited the request.</exception>
  /// <exception cref="InvalidOperationException">The client identifier or the private key is not configured.</exception>
  public async Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(installationId);

    var key = string.Create(CultureInfo.InvariantCulture, $"github:installation-token:{installationId}");

    if (cache.TryGetValue<string>(key, out var token) && token is not null)
    {
      return token;
    }

    var gate = _locks.GetOrAdd(installationId, static _ => new SemaphoreSlim(1, 1));

    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

    try
    {
      if (cache.TryGetValue(key, out token) && token is not null)
      {
        return token;
      }

      var issued = await RequestInstallationTokenAsync(installationId, cancellationToken).ConfigureAwait(false);

      cache.Set(key, issued.Token, issued.ExpiresAt - RenewalMargin);

      return issued.Token;
    }
    finally
    {
      gate.Release();
    }
  }

  /// <summary>Asks GitHub for a new token of an installation.</summary>
  /// <param name="installationId">The identifier of the GitHub App installation.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The issued token and its expiry.</returns>
  /// <exception cref="BadGatewayException">GitHub refused to issue the token or answered with an unreadable payload.</exception>
  private async Task<GitHubInstallationTokenResponse> RequestInstallationTokenAsync(
    long installationId,
    CancellationToken cancellationToken)
  {
    using var client = httpClientFactory.CreateClient(GitHubHttpClientConfiguration.ClientName);
    using var request = new HttpRequestMessage(
      HttpMethod.Post,
      new Uri(string.Create(CultureInfo.InvariantCulture, $"app/installations/{installationId}/access_tokens"), UriKind.Relative)
    );
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateAppJwt());

    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    if (!response.IsSuccessStatusCode)
    {
      throw new BadGatewayException(
        GitHubErrors.Unavailable,
        string.Create(CultureInfo.InvariantCulture, $"GitHub refused the token of installation {installationId}: HTTP {(int)response.StatusCode}.")
      );
    }

    try
    {
      return await response.Content
                           .ReadFromJsonAsync(GitHubJsonContext.Default.GitHubInstallationTokenResponse, cancellationToken)
                           .ConfigureAwait(false)
             ?? throw new JsonException("The payload is empty.");
    }
    catch (JsonException exception)
    {
      throw new BadGatewayException(
        GitHubErrors.Unavailable,
        string.Create(CultureInfo.InvariantCulture, $"GitHub answered with an unreadable token for installation {installationId}."),
        exception
      );
    }
  }

  #endregion
}
