// ------------------------------------------------------------------------------------------------
// <copyright file="CultureEndpoints.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.AspNetCore.Localization;

using CodeCoverage.Application.Features.Access.Commands.SetPreferredLanguage;

using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Localization;

namespace CodeCoverage.Web.Configuration.Localization;

/// <summary>Maps the address that remembers the language chosen in the console (ADR-015).</summary>
/// <remarks>
///   A <c>GET</c> only changes a display preference, so it needs no antiforgery token: the language picker is a plain link,
///   opened signed in or not, and during the first start.
/// </remarks>
internal static class CultureEndpoints
{
  #region Fields

  /// <summary>The address that sets the language.</summary>
  public const string CulturePath = "/culture";

  /// <summary>The name of the query parameter of the language.</summary>
  public const string LanguageParameter = "lang";

  /// <summary>How long the browser keeps the language.</summary>
  private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

  #endregion

  #region Methods

  /// <summary>Maps the endpoint.</summary>
  /// <param name="endpoints">The endpoint route builder.</param>
  /// <returns>The same builder, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="endpoints" /> is <see langword="null" />.</exception>
  public static IEndpointRouteBuilder MapCultureEndpoint(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    endpoints.MapGet(CulturePath, SetCultureAsync).ExcludeFromDescription();

    return endpoints;
  }

  /// <summary>Builds the address that sets a language and comes back to a page.</summary>
  /// <param name="language">The code of the language.</param>
  /// <param name="returnUrl">The page to come back to, path and query from the root of the site.</param>
  /// <returns>The address, relative to the base of the site.</returns>
  internal static string CultureUri(string language, string returnUrl) =>
    $"{CulturePath.TrimStart('/')}?{LanguageParameter}={Uri.EscapeDataString(language)}&{AuthenticationEndpoints.ReturnUrlParameter}={Uri.EscapeDataString(returnUrl)}";

  /// <summary>Writes the culture cookie of a language of the console on a response.</summary>
  /// <param name="response">The response.</param>
  /// <param name="language">The language.</param>
  internal static void AppendCookie(HttpResponse response, ConsoleLanguage language)
  {
    response.Cookies.Append(
      CookieRequestCultureProvider.DefaultCookieName,
      CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(language.Code)),
      new CookieOptions
      {
        Path = "/",
        MaxAge = CookieLifetime,
        SameSite = SameSiteMode.Lax,
        Secure = true,
        HttpOnly = true,
        IsEssential = true,
      });
  }

  /// <summary>Remembers a language, in the account of a signed-in user too (M4.02u), then comes back to the page.</summary>
  /// <param name="context">The HTTP context.</param>
  /// <param name="sender">The sender of the messages of the Application.</param>
  /// <param name="lang">The code of the language; one the console does not speak sets nothing.</param>
  /// <param name="returnUrl">The page to come back to; an address that is not a local path goes home.</param>
  /// <returns>The redirection.</returns>
  private static async Task<IResult> SetCultureAsync(HttpContext context, ISender sender, string? lang, string? returnUrl)
  {
    if (ConsoleLanguages.Find(lang) is { } language)
    {
      AppendCookie(context.Response, language);

      if (context.User.Identity is { IsAuthenticated: true })
      {
        await sender.Send(new SetPreferredLanguageCommand(language.Code), context.RequestAborted).ConfigureAwait(false);
      }
    }

    return Results.Redirect(AuthenticationEndpoints.LocalOrHome(returnUrl));
  }

  #endregion
}
