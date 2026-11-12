// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubSignInEvents.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

using Mediator;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

using CodeCoverage.Application.Features.Access;
using CodeCoverage.Application.Features.Access.Commands.SignInUser;
using CodeCoverage.Domain.Ports;

using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;

namespace CodeCoverage.Web.Configuration.Authentication;

/// <summary>
///   Ends the OAuth flow of GitHub: hands the tokens to <see cref="SignInUserCommand" /> and replaces the principal the
///   handler built by the principal of a session.
/// </summary>
/// <param name="sender">The sender of the request scope.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     The handler keeps no token (<c>SaveTokens = false</c>): they are protected in the row of the user, on the server
///     side, and never reach the cookie. The lifetimes come from the token response, <c>refresh_token_expires_in</c>
///     included, which the handler does not type.
///   </para>
///   <para>
///     A refusal of the sign-in issues no cookie: an account that reaches no repository is sent to the access-denied
///     page, any other failure back to the sign-in page. Neither the tokens nor the message of the failure are logged.
///   </para>
/// </remarks>
internal sealed partial class GitHubSignInEvents(ISender sender, IClock clock, ILogger<GitHubSignInEvents> logger) : OAuthEvents
{
  #region Fields

  /// <summary>Where an account that reaches no repository is sent.</summary>
  public const string NoRepositoryPath = "/access-denied?reason=" + AccessDeniedReasons.NoRepository;

  /// <summary>Where any other failure of the sign-in is sent.</summary>
  public const string FailedPath = AuthenticationEndpoints.SignInPath + "?error=true";

  /// <summary>The field of the token response that tells how long the refresh token lasts, in seconds.</summary>
  private const string RefreshTokenExpiresIn = "refresh_token_expires_in";

  #endregion

  #region Methods

  /// <summary>Reads a lifetime in seconds from the token response.</summary>
  /// <param name="response">The token response, or <see langword="null" />.</param>
  /// <param name="name">The name of the field.</param>
  /// <returns>The lifetime, or <see langword="null" /> when the field is absent, not a number or not positive.</returns>
  internal static TimeSpan? ReadLifetime(JsonDocument? response, string name)
  {
    if (response is null || !response.RootElement.TryGetProperty(name, out var value))
    {
      return null;
    }

    var seconds = value.ValueKind switch
    {
      JsonValueKind.Number when value.TryGetInt64(out var number) => number,
      JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) => number,
      var _ => 0L,
    };

    return seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
  }

  [LoggerMessage(EventId = 5100, Level = LogLevel.Warning, Message = "The sign-in with GitHub failed: {FailureType} {ErrorCode}")]
  private static partial void LogSignInFailed(ILogger logger, string failureType, string? errorCode);

  #endregion

  #region Overrides of OAuthEvents

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  public override async Task CreatingTicket(OAuthCreatingTicketContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    var command = new SignInUserCommand(
      context.AccessToken ?? string.Empty,
      context.ExpiresIn,
      context.RefreshToken,
      ReadLifetime(context.TokenResponse.Response, RefreshTokenExpiresIn)
    );

    // A refusal throws: the handler reports it to RemoteFailure, and no ticket is issued.
    var userId = await sender.Send(command, context.HttpContext.RequestAborted).ConfigureAwait(false);

    context.Principal = SessionPrincipal.Create(userId, clock.UtcNow);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  public override Task RemoteFailure(RemoteFailureContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    var refusal = context.Failure as ApplicationException;

    LogSignInFailed(logger, context.Failure?.GetType().Name ?? "unknown", refusal?.ErrorCode);

    context.Response.Redirect(refusal?.ErrorCode == AccessErrors.NoRepository ? NoRepositoryPath : FailedPath);
    context.HandleResponse();

    return Task.CompletedTask;
  }

  #endregion
}
