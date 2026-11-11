// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubUserTokenProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.GitHub.Models;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Gives the token of a user and renews it with its refresh token, which GitHub accepts once.</summary>
/// <param name="scopes">What opens the scope of each reading and of each save of a user.</param>
/// <param name="httpClientFactory">The factory of the client of the web host of GitHub.</param>
/// <param name="options">The settings of the App, read at each use.</param>
/// <param name="protector">The secret protector.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The database is the store: nothing is cached here, and a token is decrypted at the moment it is given. One
///     renewal runs at a time per user in this process; a caller that waited reads the row again and finds the
///     token the other one stored.
///   </para>
///   <para>
///     The lock does not cross processes, and the Api and the Web both renew. When two of them send the same
///     refresh token, GitHub accepts one and refuses the other: the one refused reads the row again and takes the
///     pair the other stored. The new pair is never dropped: GitHub invalidated the old one as soon as it
///     answered, so a save that loses a race on the row is made again on the fresh row.
///   </para>
///   <para>
///     Only a refresh token GitHub declares dead, or one that is missing or expired, ends the session: the tokens
///     are cleared and the user is unauthenticated. They are cleared only when the row still holds the pair that was
///     refused: a pair another process stored meanwhile is kept, and given. A failure of GitHub, or of the
///     configuration of the App, signs nobody out.
///   </para>
///   <para>
///     Once the refresh token is sent, the caller can no longer cancel: GitHub may have issued the new pair and
///     invalidated the old one, so the answer is read and saved whatever the caller does, within the timeout of the
///     client.
///   </para>
/// </remarks>
internal sealed class GitHubUserTokenProvider(
  IServiceScopeFactory scopes,
  IHttpClientFactory httpClientFactory,
  IOptionsMonitor<GitHubOptions> options,
  ISecretProtector protector,
  IClock clock) : IGitHubUserTokenProvider
{
  #region Fields

  /// <summary>The refusal of a refresh token that was already used, was revoked or has expired.</summary>
  private const string BadRefreshToken = "bad_refresh_token";

  /// <summary>How many times the save of a new pair is made again when the row changed meanwhile.</summary>
  private const int MaxSaveAttempts = 3;

  /// <summary>How many times the row is read again after a refusal, to find the pair another process stored.</summary>
  private const int WinnerReadings = 3;

  /// <summary>How long before its expiry a user token is renewed.</summary>
  private static readonly TimeSpan RenewalMargin = TimeSpan.FromMinutes(5);

  /// <summary>How long another process is given to save its pair before the row is read again.</summary>
  private static readonly TimeSpan WinnerDelay = TimeSpan.FromMilliseconds(150);

  private static readonly Uri TokenEndpoint = new("login/oauth/access_token", UriKind.Relative);

  private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

  #endregion

  #region Methods

  /// <summary>Builds the refusal of a user that must sign in again.</summary>
  /// <returns>The exception.</returns>
  private static UnauthorizedException Unauthenticated() =>
    new(SecurityErrors.Unauthenticated, "The session with GitHub has expired: sign in again.");

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
                 ?? throw Unauthenticated();

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
      return await ClearAsync(userId, user, cancellationToken).ConfigureAwait(false) ?? throw Unauthenticated();
    }

    var refreshToken = protector.Unprotect(user.RefreshTokenProtected);

    cancellationToken.ThrowIfCancellationRequested();

    // Sent, the refresh token may be spent: from here on the caller's token is not followed.
    var issued = await RequestAsync(refreshToken, CancellationToken.None).ConfigureAwait(false);

    if (issued is not null)
    {
      return await StoreAsync(userId, issued, CancellationToken.None).ConfigureAwait(false);
    }

    // Refused: another process may have used this refresh token first, and stores its pair.
    for (var reading = 1; reading <= WinnerReadings; reading++)
    {
      var (current, token) = await ReadAsync(userId, cancellationToken).ConfigureAwait(false);

      if (token is not null)
      {
        return token;
      }

      if (current.RefreshTokenProtected is null)
      {
        break;
      }

      await Task.Delay(WinnerDelay, cancellationToken).ConfigureAwait(false);
    }

    // The reading of the clearing is the last one: a pair stored after the delay is still found.
    return await ClearAsync(userId, user, cancellationToken).ConfigureAwait(false) ?? throw Unauthenticated();
  }

  /// <summary>Asks GitHub for a new pair of tokens.</summary>
  /// <param name="refreshToken">The refresh token.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The new pair, or <see langword="null" /> when GitHub declares the refresh token dead.</returns>
  /// <exception cref="BadGatewayException">GitHub does not answer, refused for another reason or answered with an unreadable payload.</exception>
  private async Task<GitHubUserTokenResponse?> RequestAsync(string refreshToken, CancellationToken cancellationToken)
  {
    var settings = options.CurrentValue;

    InvalidOperationException.ThrowIf(
      string.IsNullOrWhiteSpace(settings.ClientId),
      $"The setting '{SettingDefinitions.GitHubClientId}' is not configured."
    );

    using var client = httpClientFactory.CreateClient(GitHubHttpClientConfiguration.OAuthClientName);
    using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);

    request.Content = new FormUrlEncodedContent(
      [
        new KeyValuePair<string, string>("client_id", settings.ClientId!),
        new KeyValuePair<string, string>("client_secret", settings.GetClientSecret(protector)),
        new KeyValuePair<string, string>("grant_type", "refresh_token"),
        new KeyValuePair<string, string>("refresh_token", refreshToken),
      ]
    );

    try
    {
      using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

      if (!response.IsSuccessStatusCode)
      {
        throw new BadGatewayException(GitHubErrors.Unavailable, $"GitHub refused the renewal of a user token: HTTP {(int)response.StatusCode}.");
      }

      var issued = await response.Content.ReadFromJsonAsync(GitHubJsonContext.Default.GitHubUserTokenResponse, cancellationToken).ConfigureAwait(false)
                   ?? throw new JsonException("The payload is empty.");

      if (issued.Error is BadRefreshToken)
      {
        return null;
      }

      // Any other refusal is about the App, not about the user: nobody is signed out for it.
      return issued is { Error: null, AccessToken.Length: > 0 }
               ? issued
               : throw new BadGatewayException(GitHubErrors.Unavailable, $"GitHub refused the renewal of a user token: '{issued.Error}'.");
    }
    catch (JsonException exception)
    {
      throw new BadGatewayException(GitHubErrors.Unavailable, "GitHub answered the renewal of a user token with an unreadable payload.", exception);
    }
    catch (HttpRequestException exception)
    {
      throw new BadGatewayException(GitHubErrors.Unavailable, "GitHub does not answer the renewal of a user token.", exception);
    }
    catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
    {
      throw new BadGatewayException(GitHubErrors.Unavailable, "GitHub does not answer the renewal of a user token.", exception);
    }
  }

  /// <summary>Stores a new pair of tokens on the row of the user.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="issued">The pair GitHub issued.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The new user token.</returns>
  /// <remarks>
  ///   The old pair is dead already. A save refused because the row changed, by a sign-in for instance, is made
  ///   again on the row read afresh.
  /// </remarks>
  private async Task<string> StoreAsync(long userId, GitHubUserTokenResponse issued, CancellationToken cancellationToken)
  {
    var now = clock.UtcNow;

    for (var attempt = 1; ; attempt++)
    {
      var scope = scopes.CreateAsyncScope();

      await using (scope.ConfigureAwait(false))
      {
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.FindAsync(userId, cancellationToken).ConfigureAwait(false) ?? throw Unauthenticated();

        user.ReplaceTokens(
          protector,
          issued.AccessToken!,
          issued.ExpiresIn is { } lifetime ? now.AddSeconds(lifetime) : null,
          issued.RefreshToken,
          issued.RefreshTokenExpiresIn is { } refreshLifetime ? now.AddSeconds(refreshLifetime) : null
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

  /// <summary>Forgets the tokens of a user that GitHub no longer accepts, when the row still holds them.</summary>
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

  #region Implementation of IGitHubUserTokenProvider

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">The user has no token GitHub accepts: it must sign in again.</exception>
  /// <exception cref="BadGatewayException">GitHub does not answer the renewal, or refused it for a reason that is not about the user.</exception>
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
