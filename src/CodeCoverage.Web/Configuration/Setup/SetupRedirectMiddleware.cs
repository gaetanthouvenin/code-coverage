// ------------------------------------------------------------------------------------------------
// <copyright file="SetupRedirectMiddleware.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Web.Configuration.Setup;

/// <summary>Leads every page to the first-start wizard until the GitHub App exists, and keeps a token available for it.</summary>
/// <param name="next">The next middleware.</param>
/// <param name="configuration">The configuration, which holds the settings of every source.</param>
/// <param name="issuer">The issuer of the setup token.</param>
/// <param name="cookie">The cookie that carries the token of the wizard.</param>
/// <remarks>
///   <para>
///     The redirection never holds the token: whoever opens the site must find it in the logs. A <c>GitHub__AppId</c>
///     set in the environment makes the wizard optional: nothing is redirected, but a token is still issued while the
///     setup is unfinished, so a restart in the middle of it never leaves the operator without one.
///   </para>
///   <para>
///     A token given in the address (<c>/setup?token=…</c>) is checked here, moved into the protected cookie of the
///     wizard and dropped from the address: it is never kept in the history of the browser nor in a log of a proxy.
///   </para>
/// </remarks>
internal sealed class SetupRedirectMiddleware(RequestDelegate next, IConfiguration configuration, ISetupTokenIssuer issuer, SetupCookie cookie)
{
  #region Fields

  /// <summary>The address of the wizard.</summary>
  private const string SetupPath = "/setup";

  /// <summary>The name of the query parameter that carries the token on the first visit.</summary>
  private const string TokenParameter = "token";

  /// <summary>The addresses that work before the setup: the wizard, the probes, the error pages and the framework.</summary>
  private static readonly string[] OpenPaths = [SetupPath, "/health", "/error", "/not-found", "/_framework", "/_blazor", "/_content"];

  /// <summary>The settings the setup fills: while one is missing, a token is kept available.</summary>
  private static readonly string[] SetupKeys =
  [
    SettingDefinitions.GitHubAppId,
    SettingDefinitions.UrlsWebPublicUrl,
    SettingDefinitions.UrlsApiPublicUrl,
    SettingDefinitions.AccessAdminUserIds,
  ];

  #endregion

  #region Methods

  /// <summary>Handles a request.</summary>
  /// <param name="context">The context of the request.</param>
  /// <returns>A task that completes when the request is handled.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  public Task InvokeAsync(HttpContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    if (IsComplete())
    {
      return next(context);
    }

    issuer.EnsureIssued();

    if (TakesTokenFromTheAddress(context, out var token))
    {
      // A valid token moves to the cookie; an invalid one is simply dropped, and the wizard says so.
      if (issuer.IsValid(token))
      {
        cookie.Write(context, token, state: null);
      }

      context.Response.Redirect(SetupPath);

      return Task.CompletedTask;
    }

    if (IsOpen(context.Request) || !string.IsNullOrEmpty(configuration[SettingDefinitions.GitHubAppId]))
    {
      return next(context);
    }

    context.Response.Redirect(SetupPath);

    return Task.CompletedTask;
  }

  /// <summary>Tells whether a request is served before the setup.</summary>
  /// <param name="request">The request.</param>
  /// <returns><see langword="true" /> for anything but the navigation to a page.</returns>
  private static bool IsOpen(HttpRequest request) =>
    !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
    || Path.HasExtension(request.Path.Value)
    || Array.Exists(OpenPaths, open => request.Path.StartsWithSegments(open, StringComparison.OrdinalIgnoreCase));

  /// <summary>Tells whether the request carries a setup token in the address of the wizard.</summary>
  /// <param name="context">The context of the request.</param>
  /// <param name="token">The token read, when there is one.</param>
  /// <returns><see langword="true" /> when the address of the wizard carries a token.</returns>
  private static bool TakesTokenFromTheAddress(HttpContext context, out string token)
  {
    token = string.Empty;

    if (!HttpMethods.IsGet(context.Request.Method) || !context.Request.Path.StartsWithSegments(SetupPath, StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    if (!context.Request.Query.TryGetValue(TokenParameter, out var values) || values.FirstOrDefault() is not { Length: > 0 } value)
    {
      return false;
    }

    token = value;

    return true;
  }

  /// <summary>Tells whether the setup left nothing to do.</summary>
  /// <returns><see langword="true" /> when every setting the wizard fills has a value.</returns>
  private bool IsComplete() => Array.TrueForAll(SetupKeys, key => !string.IsNullOrEmpty(configuration[key]));

  #endregion
}
