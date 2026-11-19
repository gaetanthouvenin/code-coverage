// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsUserTokenProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Polly;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.AzureDevOps.Models;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.AzureDevOps;

/// <summary>Gives the delegated token of an Azure DevOps user and renews it with its refresh token, through Microsoft Entra ID.</summary>
/// <param name="scopes">What opens the scope of each reading and of each save of a user.</param>
/// <param name="httpClientFactory">The factory of the client of Microsoft Entra ID.</param>
/// <param name="options">The <c>AzureDevOps</c> settings, read at each renewal so that a new secret applies at once.</param>
/// <param name="protector">The secret protector.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     It is the <see cref="IForgeUserTokenProvider" /> of Azure DevOps, keyed by <see cref="Domain.Organizations.Forge.AzureDevOps" />,
///     which <see cref="IForgeGatewayProvider.GetUserTokenProvider" /> resolves. The tokens are the ones the sign-in by
///     Entra ID stored on the row of the user (M7.53), protected.
///   </para>
///   <para>
///     The database is the store: nothing is cached here, and a token is decrypted at the moment it is given. One
///     renewal runs at a time per user in this process; a caller that waited reads the row again and finds the token
///     the other one stored. The renewal is a <c>refresh_token</c> request of the application of the instance, a
///     confidential client, on the <c>organizations</c> endpoint of the authority: only work and school accounts sign
///     in (ADR-014). Entra ID may rotate the refresh token; when it gives none, the one sent is kept.
///   </para>
///   <para>
///     Only a refresh token Entra ID declares dead (<c>invalid_grant</c>), or one that is missing or expired, ends the
///     session: the tokens are cleared and the user is unauthenticated. They are cleared only when the row still holds
///     the pair that was refused: a pair another process or a sign-in stored meanwhile is kept, and given. A failure of
///     Entra ID, or of the configuration of the application, signs nobody out.
///   </para>
///   <para>
///     Once the refresh token is sent, the caller can no longer cancel: the answer is read and saved whatever the caller
///     does, within the timeout of the client. No token is ever logged: the answer of Entra ID masks its tokens.
///   </para>
/// </remarks>
internal sealed class AzureDevOpsUserTokenProvider(
  IServiceScopeFactory scopes,
  IHttpClientFactory httpClientFactory,
  IOptionsMonitor<AzureDevOpsOptions> options,
  ISecretProtector protector,
  IClock clock) : IForgeUserTokenProvider
{
  #region Fields

  /// <summary>The scopes asked for at a renewal: the delegated permissions of Azure DevOps, and a new refresh token.</summary>
  public const string Scope = AzureDevOpsTokenProvider.Scope + " offline_access";

  /// <summary>The refusal of a refresh token that was revoked, has expired or needs the user to sign in again.</summary>
  private const string InvalidGrant = "invalid_grant";

  private const string ForgeName = "Azure DevOps";

  /// <summary>How many times the save of a new pair is made again when the row changed meanwhile.</summary>
  private const int MaxSaveAttempts = 3;

  /// <summary>How long before its expiry a user token is renewed.</summary>
  private static readonly TimeSpan RenewalMargin = TimeSpan.FromMinutes(5);

  private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

  #endregion

  #region Methods

  /// <summary>Gets the token endpoint of the work and school accounts of every tenant.</summary>
  /// <param name="settings">The <c>AzureDevOps</c> settings.</param>
  /// <returns>The absolute address of the OAuth 2.0 v2 token endpoint of <c>organizations</c>.</returns>
  private static Uri TokenEndpointOf(AzureDevOpsOptions settings) =>
    new($"{settings.Authority.TrimEnd('/')}/organizations/oauth2/v2.0/token", UriKind.Absolute);

  /// <summary>Tells whether the stored token of a user can still be given.</summary>
  /// <param name="user">The user.</param>
  /// <returns><see langword="true" /> for a token that does not expire within <see cref="RenewalMargin" />.</returns>
  private bool IsUsable(AppUser user) =>
    user.AccessTokenProtected is not null && (user.AccessTokenExpiresAt is not { } expiry || expiry - RenewalMargin > clock.UtcNow);

  /// <summary>Reads the stored token of a user, when it can still be given.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The user and its token, which is <see langword="null" /> when it must be renewed.</returns>
  /// <exception cref="UnauthorizedException">The user does not exist.</exception>
  private async Task<(AppUser User, string? Token)> ReadAsync(long userId, CancellationToken cancellationToken)
  {
    var scope = scopes.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>().FindAsync(userId, cancellationToken).ConfigureAwait(false)
                 ?? throw UnauthorizedException.UserSessionExpired(ForgeName);

      return (user, IsUsable(user) ? protector.Unprotect(user.AccessTokenProtected!) : null);
    }
  }

  /// <summary>Renews the token of a user, under the lock of that user.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The token to give.</returns>
  private async Task<string> RefreshAsync(long userId, CancellationToken cancellationToken)
  {
    // Read again under the lock: the caller that held it may have renewed the token already.
    var (user, usable) = await ReadAsync(userId, cancellationToken).ConfigureAwait(false);

    if (usable is not null)
    {
      return usable;
    }

    if (user.RefreshTokenProtected is null || user.RefreshTokenExpiresAt <= clock.UtcNow)
    {
      return await ClearAsync(userId, user, cancellationToken).ConfigureAwait(false) ?? throw UnauthorizedException.UserSessionExpired(ForgeName);
    }

    var refreshToken = protector.Unprotect(user.RefreshTokenProtected);

    cancellationToken.ThrowIfCancellationRequested();

    // Sent, the refresh token may be rotated: from here on the caller's token is not followed.
    var issued = await RequestAsync(refreshToken, CancellationToken.None).ConfigureAwait(false);

    if (issued is not null)
    {
      return await StoreAsync(userId, issued, refreshToken, CancellationToken.None).ConfigureAwait(false);
    }

    // Refused: the clearing keeps a pair another process or a sign-in stored meanwhile.
    return await ClearAsync(userId, user, cancellationToken).ConfigureAwait(false) ?? throw UnauthorizedException.UserSessionExpired(ForgeName);
  }

  /// <summary>Asks Microsoft Entra ID for a new user token.</summary>
  /// <param name="refreshToken">The refresh token.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The answer, or <see langword="null" /> when Entra ID declares the refresh token dead.</returns>
  /// <exception cref="BadGatewayException">Entra ID does not answer, refused for another reason or answered with an unreadable payload.</exception>
  /// <exception cref="InvalidOperationException">The client identifier, or both the secret and the certificate, are not configured.</exception>
  private async Task<EntraUserTokenResponse?> RequestAsync(string refreshToken, CancellationToken cancellationToken)
  {
    var settings = options.CurrentValue;
    var tokenEndpoint = TokenEndpointOf(settings);
    var form = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["grant_type"] = "refresh_token",
      ["refresh_token"] = refreshToken,
      ["scope"] = Scope,
    };

    EntraClientAuthentication.AddTo(form, settings, tokenEndpoint, protector, clock.UtcNow);

    using var client = httpClientFactory.CreateClient(AzureDevOpsHttpClientConfiguration.EntraClientName);
    using var content = new FormUrlEncodedContent(form);

    try
    {
      using var response = await client.PostAsync(tokenEndpoint, content, cancellationToken).ConfigureAwait(false);

      // Entra ID answers a refusal with a 400 and its code: read it before judging the status.
      var answer = response.StatusCode is HttpStatusCode.BadRequest || response.IsSuccessStatusCode
                     ? await response.Content.ReadFromJsonAsync(AzureDevOpsJsonContext.Default.EntraUserTokenResponse, cancellationToken).ConfigureAwait(false)
                     : null;

      if (answer?.Error is InvalidGrant)
      {
        return null;
      }

      // Any other refusal is about the application, not about the user: nobody is signed out for it.
      BadGatewayException.ThrowIfForgeRefused(
        !response.IsSuccessStatusCode || answer is not { Error: null, AccessToken.Length: > 0 },
        string.Create(
          CultureInfo.InvariantCulture,
          $"Microsoft Entra ID refused the renewal of a user token: HTTP {(int)response.StatusCode}, '{answer?.Error}'."
        )
      );

      return answer;
    }
    catch (JsonException exception)
    {
      throw BadGatewayException.ForgeUnavailable("Microsoft Entra ID answered the renewal of a user token with an unreadable payload.", exception);
    }
    catch (Exception exception) when (exception is HttpRequestException or ExecutionRejectedException)
    {
      throw BadGatewayException.ForgeUnavailable("Microsoft Entra ID does not answer the renewal of a user token.", exception);
    }
    catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
    {
      throw BadGatewayException.ForgeUnavailable("Microsoft Entra ID does not answer the renewal of a user token.", exception);
    }
  }

  /// <summary>Stores a new pair of tokens on the row of the user.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="issued">The answer of Entra ID.</param>
  /// <param name="sentRefreshToken">The refresh token that was sent, kept when Entra ID gives no new one.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The new user token.</returns>
  /// <remarks>A save refused because the row changed, by a sign-in for instance, is made again on the row read afresh.</remarks>
  private async Task<string> StoreAsync(long userId, EntraUserTokenResponse issued, string sentRefreshToken, CancellationToken cancellationToken)
  {
    var now = clock.UtcNow;

    for (var attempt = 1; ; attempt++)
    {
      var scope = scopes.CreateAsyncScope();

      await using (scope.ConfigureAwait(false))
      {
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.FindAsync(userId, cancellationToken).ConfigureAwait(false) ?? throw UnauthorizedException.UserSessionExpired(ForgeName);

        // Entra ID does not tell when a refresh token expires: it is kept without expiry until refused.
        user.ReplaceTokens(
          protector,
          issued.AccessToken!,
          issued.ExpiresIn is { } lifetime ? now.AddSeconds(lifetime) : null,
          issued.RefreshToken ?? sentRefreshToken,
          null
        );

        try
        {
          await users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

          return issued.AccessToken!;
        }
        catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange && attempt < MaxSaveAttempts)
        {
          // Another save won: the next attempt writes the pair on what it wrote.
        }
      }
    }
  }

  /// <summary>Forgets the tokens of a user that Entra ID no longer accepts, when the row still holds them.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="refused">The row as it was read when its pair was found dead.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>
  ///   The token of a pair another process stored meanwhile, which is kept; or <see langword="null" /> when the tokens
  ///   are cleared, or when no token can be given.
  /// </returns>
  private async Task<string?> ClearAsync(long userId, AppUser refused, CancellationToken cancellationToken)
  {
    var scope = scopes.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

      if (await users.FindAsync(userId, cancellationToken).ConfigureAwait(false) is not { } user)
      {
        return null;
      }

      // Another process, or a sign-in, stored a pair after the last reading: it may be the only one still alive.
      if (!string.Equals(user.AccessTokenProtected, refused.AccessTokenProtected, StringComparison.Ordinal)
          || !string.Equals(user.RefreshTokenProtected, refused.RefreshTokenProtected, StringComparison.Ordinal))
      {
        return IsUsable(user) ? protector.Unprotect(user.AccessTokenProtected!) : null;
      }

      user.ClearTokens();

      try
      {
        await users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
      }
      catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange)
      {
        // The row changed meanwhile, by another process or a sign-in that stored a new pair: it is kept, and given.
        return await ReadAsync(userId, cancellationToken).ConfigureAwait(false) is { Token: { } stored } ? stored : null;
      }

      return null;
    }
  }

  #endregion

  #region Implementation of IForgeUserTokenProvider

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">The user has no token Entra ID accepts: it must sign in again.</exception>
  /// <exception cref="BadGatewayException">Entra ID does not answer the renewal, or refused it for a reason that is not about the user.</exception>
  /// <exception cref="InvalidOperationException">The application of the instance is not configured.</exception>
  public async Task<string> GetTokenAsync(long userId, CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

    if ((await ReadAsync(userId, cancellationToken).ConfigureAwait(false)).Token is { } token)
    {
      return token;
    }

    var gate = _locks.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));

    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

    try
    {
      return await RefreshAsync(userId, cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      gate.Release();
    }
  }

  #endregion
}
