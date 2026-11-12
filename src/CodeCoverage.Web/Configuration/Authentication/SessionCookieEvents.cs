// ------------------------------------------------------------------------------------------------
// <copyright file="SessionCookieEvents.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Web.Configuration.Authentication;

/// <summary>Checks the session cookie on each request, and sets the principal of the request scope.</summary>
/// <param name="users">The repository of the users.</param>
/// <param name="currentUser">The principal of the request scope.</param>
/// <remarks>
///   <para>
///     A session goes on only while its user exists and keeps a token GitHub could accept: a renewal GitHub refused
///     clears the tokens, and the next request rejects the cookie, which leads to the sign-in page. The check reads one
///     row by its key; the fingerprinted static assets are cached by the browser and come back rarely.
///   </para>
///   <para>
///     The login is read here, never stored in the cookie: the request scope, the prerendering of the pages included,
///     then runs for that user. No organization and no administration right are set: the gate decides them for each
///     message.
///   </para>
/// </remarks>
internal sealed class SessionCookieEvents(IUserRepository users, CurrentUserContext currentUser) : CookieAuthenticationEvents
{
  #region Overrides of CookieAuthenticationEvents

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    var user = await SessionUsers.FindAsync(users, context.Principal, context.HttpContext.RequestAborted).ConfigureAwait(false);

    if (user is null)
    {
      context.RejectPrincipal();
      await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);

      return;
    }

    currentUser.SetUser(user.Id, user.Login, user.Forge, user.ExternalId);
  }

  #endregion
}
