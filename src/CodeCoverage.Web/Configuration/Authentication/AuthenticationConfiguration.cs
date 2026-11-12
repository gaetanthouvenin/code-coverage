// ------------------------------------------------------------------------------------------------
// <copyright file="AuthenticationConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using AspNet.Security.OAuth.GitHub;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Options;

using CodeCoverage.Web.Configuration.Identity;

namespace CodeCoverage.Web.Configuration.Authentication;

/// <summary>Registers the session cookie and the sign-in with GitHub (ARCHITECTURE §10bis.2).</summary>
public static class AuthenticationConfiguration
{
  #region Fields

  /// <summary>The name of the session cookie.</summary>
  public const string SessionCookieName = "cc.session";

  /// <summary>How long a session lasts without any request, renewed while it is used.</summary>
  public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);

  #endregion

  #region Methods

  /// <summary>Registers the authentication, the authorization and the identity of the circuits.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   <para>
  ///     The cookie holds the internal identifier of the user and the time of the sign-in only, protected by the key ring
  ///     shared through the database: Data Protection must be registered first. The GitHub handler keeps no token; its
  ///     client identifier and secret follow the settings (<see cref="ConfigureGitHubOAuthOptions" />).
  ///   </para>
  ///   <para>
  ///     No page requires a session yet: an anonymous request is only asked to sign in by the challenge of a later
  ///     authorization (M3.19).
  ///   </para>
  /// </remarks>
  public static IServiceCollection AddCodeCoverageAuthentication(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
              o.Cookie.Name = SessionCookieName;
              o.Cookie.HttpOnly = true;
              o.Cookie.SameSite = SameSiteMode.Lax;
              o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
              o.ExpireTimeSpan = SessionLifetime;
              o.SlidingExpiration = true;
              o.LoginPath = AuthenticationEndpoints.SignInPath;
              o.LogoutPath = AuthenticationEndpoints.SignOutPath;
              o.AccessDeniedPath = "/access-denied";
              o.ReturnUrlParameter = AuthenticationEndpoints.ReturnUrlParameter;
              o.EventsType = typeof(SessionCookieEvents);
            })
            .AddGitHub(o =>
            {
              // The tokens are protected in the row of the user, never in the cookie.
              o.SaveTokens = false;

              // A GitHub App ignores the scopes: the permissions of the App decide what a user token may read.
              o.Scope.Clear();
              o.EventsType = typeof(GitHubSignInEvents);
            });

    services.AddScoped<SessionCookieEvents>();
    services.AddScoped<GitHubSignInEvents>();
    services.AddSingleton<ConfigureGitHubOAuthOptions>();
    services.AddSingleton<IConfigureOptions<GitHubAuthenticationOptions>>(static sp => sp.GetRequiredService<ConfigureGitHubOAuthOptions>());

    services.AddAuthorization();
    services.AddCascadingAuthenticationState();
    services.AddScoped<CircuitHandler, CircuitIdentityHandler>();

    return services;
  }

  #endregion
}
