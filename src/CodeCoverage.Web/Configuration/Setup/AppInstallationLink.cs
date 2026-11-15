// ------------------------------------------------------------------------------------------------
// <copyright file="AppInstallationLink.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Web.Configuration.Setup;

/// <summary>Builds the address where an account installs the GitHub App of the instance.</summary>
/// <param name="github">The settings of GitHub, read at each call.</param>
/// <remarks>
///   The address is <c>https://github.com/apps/{GitHub:AppSlug}/installations/new</c>: GitHub asks there which account
///   installs the App and on which repositories. The slug is read at each call, so the link appears as soon as the
///   wizard saves the App, without a restart.
/// </remarks>
internal sealed class AppInstallationLink(IOptionsMonitor<GitHubOptions> github)
{
  #region Fields

  /// <summary>The root of the pages of the GitHub Apps.</summary>
  private const string AppsRoot = "https://github.com/apps/";

  #endregion

  #region Properties

  /// <summary>Gets the address that installs the App, or <see langword="null" /> while the App has no slug.</summary>
  public Uri? Current =>
    github.CurrentValue.AppSlug is { } slug && !string.IsNullOrWhiteSpace(slug)
      ? new Uri($"{AppsRoot}{Uri.EscapeDataString(slug.Trim())}/installations/new")
      : null;

  #endregion
}
