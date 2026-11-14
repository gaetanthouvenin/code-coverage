// ------------------------------------------------------------------------------------------------
// <copyright file="AuthenticationEndpoints.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using AspNet.Security.OAuth.GitHub;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Web.Configuration.Authentication;

/// <summary>
///   Maps the endpoints of the authentication framework: the start of the sign-in with GitHub and the sign-out. The
///   pages stay Razor components; these two only talk to the authentication handlers.
/// </summary>
internal static class AuthenticationEndpoints
{
  #region Fields

  /// <summary>The address of the sign-in page.</summary>
  public const string SignInPath = "/signin";

  /// <summary>The address that starts the sign-in with GitHub.</summary>
  public const string ChallengePath = "/signin/github";

  /// <summary>The address of the sign-out, a form post.</summary>
  public const string SignOutPath = "/signout";

  /// <summary>The name of the query parameter of the address to return to after the sign-in.</summary>
  public const string ReturnUrlParameter = "returnUrl";

  #endregion

  #region Methods

  /// <summary>Maps the endpoints.</summary>
  /// <param name="endpoints">The endpoint route builder.</param>
  /// <returns>The same builder, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="endpoints" /> is <see langword="null" />.</exception>
  public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    endpoints.MapGet(ChallengePath, Challenge).ExcludeFromDescription();
    endpoints.MapPost(SignOutPath, SignOutAsync).ExcludeFromDescription();

    return endpoints;
  }

  /// <summary>Keeps an address to return to only when it stays on this site.</summary>
  /// <param name="returnUrl">The address, or <see langword="null" />.</param>
  /// <returns>The address when it is a local path, else the home page.</returns>
  /// <remarks>An absolute address, a protocol-relative one (<c>//host</c>) or one with a backslash could leave the site.</remarks>
  internal static string LocalOrHome(string? returnUrl) =>
    returnUrl is { Length: > 0 }
    && returnUrl[0] == '/'
    && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
    && !returnUrl.Contains('\\', StringComparison.Ordinal)
    && !returnUrl.Any(char.IsControl)
      ? returnUrl
      : "/";

  /// <summary>Builds the address of the sign-in page that brings the user back to a page of this site.</summary>
  /// <param name="returnUrl">The address of the page, path and query from the root of the site; any other goes home.</param>
  /// <param name="expired">Whether the sign-in page says the session expired.</param>
  /// <returns>The address, relative to the base of the site.</returns>
  internal static string SignInUri(string? returnUrl, bool expired = false)
  {
    var uri = $"{SignInPath.TrimStart('/')}?{ReturnUrlParameter}={Uri.EscapeDataString(LocalOrHome(returnUrl))}";

    return expired ? $"{uri}&expired=true" : uri;
  }

  /// <summary>Starts the sign-in with GitHub.</summary>
  /// <param name="returnUrl">The address to return to, or <see langword="null" />.</param>
  /// <param name="github">The settings of GitHub.</param>
  /// <returns>The challenge, or a return to the sign-in page while the App has no client identifier.</returns>
  private static IResult Challenge(string? returnUrl, IOptionsMonitor<GitHubOptions> github)
  {
    if (string.IsNullOrWhiteSpace(github.CurrentValue.ClientId))
    {
      return Results.LocalRedirect(GitHubSignInEvents.FailedPath);
    }

    var properties = new AuthenticationProperties { RedirectUri = LocalOrHome(returnUrl) };

    return Results.Challenge(properties, [GitHubAuthenticationDefaults.AuthenticationScheme]);
  }

  /// <summary>Ends the session.</summary>
  /// <param name="context">The HTTP context.</param>
  /// <param name="antiforgery">The antiforgery service, which the form must satisfy.</param>
  /// <returns>A return to the sign-in page, or 400 without a valid antiforgery token.</returns>
  private static async Task<IResult> SignOutAsync(HttpContext context, IAntiforgery antiforgery)
  {
    if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false))
    {
      return Results.BadRequest();
    }

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);

    return Results.LocalRedirect(SignInPath);
  }

  #endregion
}
