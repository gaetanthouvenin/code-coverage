// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenAuthenticationHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;

using Mediator;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

using CodeCoverage.Api.Configuration.Identity;
using CodeCoverage.Application.Features.Access.Queries.AuthenticateApiToken;

namespace CodeCoverage.Api.Configuration.Authentication;

/// <summary>Authenticates a request by a personal token of the API: <c>Authorization: Bearer cvp_…</c>.</summary>
/// <param name="options">The options of the scheme.</param>
/// <param name="logger">The logger factory.</param>
/// <param name="encoder">The URL encoder.</param>
/// <param name="sender">The sender of the request scope.</param>
/// <remarks>
///   <para>
///     A bearer value without the <c>cvp_</c> prefix is left alone (no result): the upload tokens (<c>cvc_…</c>) and the
///     imported Codecov tokens travel in the same header and are checked by the uploads themselves. An unknown, revoked or
///     expired token fails, and the challenge answers 401 with <c>WWW-Authenticate: Bearer</c>.
///   </para>
///   <para>
///     The principal names the user of the token and the organization the token is bound to; it never carries a role.
///     The access checks run for that user, with the GitHub token stored for it since its sign-in on the Web, never with
///     a token sent by the caller. The handler enters no organization: the gate decides it for each message.
///   </para>
/// </remarks>
internal sealed class ApiTokenAuthenticationHandler(
  IOptionsMonitor<AuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  UrlEncoder encoder,
  ISender sender) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
  #region Fields

  /// <summary>The name of the scheme.</summary>
  public const string SchemeName = "ApiToken";

  /// <summary>The prefix of a personal token of the API.</summary>
  public const string TokenPrefix = "cvp_";

  /// <summary>The value of <see cref="HttpCurrentUserMiddleware.TokenOrganizationClaim" /> for a token of the instance.</summary>
  public const string InstanceToken = "instance";

  /// <summary>The scheme of the <c>Authorization</c> header.</summary>
  private const string BearerPrefix = "Bearer ";

  #endregion

  #region Methods

  /// <summary>Reads a personal token of the API from the <c>Authorization</c> header.</summary>
  /// <param name="header">The value of the header.</param>
  /// <returns>The token, or <see langword="null" /> when the header carries none.</returns>
  internal static string? ReadToken(string? header)
  {
    if (header is null || !header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
      return null;
    }

    var value = header[BearerPrefix.Length..].Trim();

    return value.StartsWith(TokenPrefix, StringComparison.Ordinal) ? value : null;
  }

  #endregion

  #region Overrides of AuthenticationHandler<AuthenticationSchemeOptions>

  /// <inheritdoc />
  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (ReadToken(Request.Headers.Authorization.ToString()) is not { } token)
    {
      return AuthenticateResult.NoResult();
    }

    if (await sender.Send(new AuthenticateApiTokenQuery(token), Context.RequestAborted).ConfigureAwait(false) is not { } identity)
    {
      return AuthenticateResult.Fail("The API token is unknown, revoked or expired.");
    }

    var claims = new List<Claim>
    {
      new(ClaimTypes.NameIdentifier, identity.UserId.ToString(CultureInfo.InvariantCulture)),
      new(ClaimTypes.Name, identity.Login),
      new(HttpCurrentUserMiddleware.ForgeClaim, identity.Forge.ToString()),
      new(HttpCurrentUserMiddleware.ExternalIdClaim, identity.ExternalId),
      new(HttpCurrentUserMiddleware.TokenOrganizationClaim, identity.OrganizationId?.ToString(CultureInfo.InvariantCulture) ?? InstanceToken),
    };

    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));

    return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
  }

  /// <inheritdoc />
  protected override Task HandleChallengeAsync(AuthenticationProperties properties)
  {
    Response.StatusCode = StatusCodes.Status401Unauthorized;
    Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer";

    return Task.CompletedTask;
  }

  #endregion
}
