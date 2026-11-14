// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppManifest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json.Nodes;

namespace CodeCoverage.Domain.Settings;

/// <summary>The manifest that creates the GitHub App of an instance in one click.</summary>
/// <remarks>
///   The App is public, shared by every organization (ADR-013). <c>installation</c> and
///   <c>installation_repositories</c> are not listed: GitHub delivers them to every App without a subscription.
/// </remarks>
public static class GitHubAppManifest
{
  #region Fields

  private static readonly string[] Events =
  [
    "installation_target",
    "organization",
    "member",
    "membership",
    "pull_request",
    "push",
    "repository",
    "delete",
  ];

  #endregion

  #region Methods

  /// <summary>Writes the manifest.</summary>
  /// <param name="appName">The name of the App, unique on GitHub.</param>
  /// <param name="webPublicUrl">The public address of the Web host.</param>
  /// <param name="apiPublicUrl">The public address of the Api host.</param>
  /// <returns>The manifest, as JSON.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="appName" /> is empty or white space.</exception>
  public static string ToJson(string appName, Uri webPublicUrl, Uri apiPublicUrl)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(appName);
    ArgumentNullException.ThrowIfNull(webPublicUrl);
    ArgumentNullException.ThrowIfNull(apiPublicUrl);

    var web = webPublicUrl.AbsoluteUri.TrimEnd('/');
    var api = apiPublicUrl.AbsoluteUri.TrimEnd('/');

    var manifest = new JsonObject
    {
      ["name"] = appName,
      ["url"] = web,
      ["public"] = true,
      ["hook_attributes"] = new JsonObject { ["url"] = $"{api}/api/v1/github/webhooks", ["active"] = true },
      ["redirect_url"] = $"{web}/setup",
      ["callback_urls"] = new JsonArray($"{web}/signin-github"),
      ["request_oauth_on_install"] = false,
      ["default_permissions"] = new JsonObject
      {
        ["contents"] = "read",
        ["metadata"] = "read",
        ["statuses"] = "write",
        ["pull_requests"] = "write",
        ["checks"] = "write",
        ["members"] = "read",
      },
      ["default_events"] = new JsonArray([.. Events.Select(static e => JsonValue.Create(e))]),
    };

    return manifest.ToJsonString();
  }

  #endregion
}
