// ------------------------------------------------------------------------------------------------
// <copyright file="SetupRedirectMiddleware.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Web.Configuration.Setup;

/// <summary>Leads every page to the first-start wizard until the GitHub App exists.</summary>
/// <param name="next">The next middleware.</param>
/// <param name="configuration">The configuration, which holds the settings of every source.</param>
/// <param name="issuer">The issuer of the setup token.</param>
/// <remarks>
///   The redirection never holds the token: whoever opens the site must find it in the logs. A <c>GitHub__AppId</c> set
///   in the environment makes the wizard optional: no token is issued and nothing is redirected.
/// </remarks>
internal sealed class SetupRedirectMiddleware(RequestDelegate next, IConfiguration configuration, ISetupTokenIssuer issuer)
{
  #region Fields

  /// <summary>The address of the wizard.</summary>
  private const string SetupPath = "/setup";

  /// <summary>The addresses that work before the setup: the wizard, the probes, the error pages and the framework.</summary>
  private static readonly string[] OpenPaths = [SetupPath, "/health", "/error", "/not-found", "/_framework", "/_blazor", "/_content"];

  #endregion

  #region Methods

  /// <summary>Handles a request.</summary>
  /// <param name="context">The context of the request.</param>
  /// <returns>A task that completes when the request is handled.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  public Task InvokeAsync(HttpContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    if (!string.IsNullOrEmpty(configuration[SettingDefinitions.GitHubAppId]))
    {
      return next(context);
    }

    issuer.EnsureIssued();

    if (IsOpen(context.Request))
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

  #endregion
}
