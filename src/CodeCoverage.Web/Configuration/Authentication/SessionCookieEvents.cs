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
/// <param name="session">Records a rejected cookie, for the pages to say the session expired.</param>
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
///   <para>
///     The error page of an unhandled error is answered without reading the user: it must still render when the database is down, which
///     is often why it is shown. The cookie is kept, but the request runs signed out, so the layout reads nothing
///     either.
///   </para>
/// </remarks>
internal sealed class SessionCookieEvents(IUserRepository users, CurrentUserContext currentUser, SessionState session) : CookieAuthenticationEvents
{
  #region Fields

  /// <summary>The address the exception handler re-executes after an unhandled error.</summary>
  private const string ErrorPath = "/error";

  #endregion

  #region Overrides of CookieAuthenticationEvents

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    if (context.HttpContext.Request.Path.Equals(ErrorPath, StringComparison.OrdinalIgnoreCase))
    {
      // Neither signed out nor expired: this one request is anonymous, and the next page checks the cookie again.
      context.RejectPrincipal();

      return;
    }

    var user = await SessionUsers.FindAsync(users, context.Principal, context.HttpContext.RequestAborted).ConfigureAwait(false);

    if (user is null)
    {
      context.RejectPrincipal();
      session.Reject();
      await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);

      return;
    }

    currentUser.SetUser(user.Id, user.Login, user.Forge, user.ExternalId);
  }

  #endregion
}
