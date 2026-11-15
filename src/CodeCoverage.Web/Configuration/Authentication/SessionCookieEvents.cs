// ------------------------------------------------------------------------------------------------
// <copyright file="SessionCookieEvents.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Web.Configuration.Localization;
using CodeCoverage.Web.Localization;

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

  #region Methods

  /// <summary>
  ///   Serves a signed-in user in the language the account keeps (M4.02u), and writes it in the culture cookie of this
  ///   browser when the cookie says another one or nothing.
  /// </summary>
  /// <param name="context">The HTTP context.</param>
  /// <param name="code">The language of the account, or <see langword="null" /> when the user never chose one.</param>
  private static void KeepLanguageOfTheAccount(HttpContext context, string? code)
  {
    if (ConsoleLanguages.Find(code) is not { } language)
    {
      return;
    }

    AccountRequestCultureProvider.SetLanguage(context, language);

    var cookie = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
    var current = cookie is null ? null : CookieRequestCultureProvider.ParseCookieValue(cookie)?.UICultures.FirstOrDefault().Value;

    if (!string.Equals(current, language.Code, StringComparison.OrdinalIgnoreCase))
    {
      CultureEndpoints.AppendCookie(context.Response, language);
    }
  }

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
    KeepLanguageOfTheAccount(context.HttpContext, user.PreferredLanguage);
  }

  #endregion
}
