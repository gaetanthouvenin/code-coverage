// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubLinks.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>Builds the addresses of the pages of GitHub, from the address of its API (<c>GitHub:ApiBaseUrl</c>).</summary>
/// <param name="options">The <c>GitHub</c> settings, read at each call: a change of the API address applies without a restart.</param>
/// <remarks>
///   The site is the API address without its API part: <c>https://api.github.com</c> gives <c>https://github.com</c>, and a
///   GitHub Enterprise Server API, <c>https://ghe.example.com/api/v3</c>, gives <c>https://ghe.example.com</c>. Any other
///   address is taken as the site itself.
/// </remarks>
internal sealed class GitHubLinks(IOptionsMonitor<GitHubOptions> options) : IForgeLinks
{
  #region Fields

  // The host prefix of the public API, api.github.com, absent from the host of the site.
  private const string ApiHostPrefix = "api.";

  // The path of the API of GitHub Enterprise Server, under the host of the site.
  private const string EnterpriseApiPath = "/api/v3";

  #endregion

  #region Methods

  /// <summary>Gets the address of the GitHub site that an API address belongs to.</summary>
  /// <param name="apiBaseAddress">The absolute address of the API.</param>
  /// <returns>The absolute address of the site, ending with a slash.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="apiBaseAddress" /> is <see langword="null" />.</exception>
  internal static Uri SiteOf(Uri apiBaseAddress)
  {
    ArgumentNullException.ThrowIfNull(apiBaseAddress);

    var builder = new UriBuilder(apiBaseAddress) { Query = string.Empty, Fragment = string.Empty };
    var path = apiBaseAddress.AbsolutePath.TrimEnd('/');

    if (path.Length == 0 && builder.Host.StartsWith(ApiHostPrefix, StringComparison.OrdinalIgnoreCase))
    {
      builder.Host = builder.Host[ApiHostPrefix.Length..];
    }
    else if (path.EndsWith(EnterpriseApiPath, StringComparison.OrdinalIgnoreCase))
    {
      path = path[..^EnterpriseApiPath.Length];
    }

    builder.Path = $"{path}/";

    return builder.Uri;
  }

  // The address of the repository on the site, each segment escaped, ending with a slash.
  private string RepositoryOf(string owner, string name)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(owner);
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    var site = SiteOf(options.CurrentValue.GetApiBaseAddress());

    return $"{site.AbsoluteUri}{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/";
  }

  #endregion

  #region Implementation of IForgeLinks

  /// <inheritdoc />
  public Uri PullRequestUrl(string owner, string name, int number) =>
    new(string.Create(CultureInfo.InvariantCulture, $"{RepositoryOf(owner, name)}pull/{number}"), UriKind.Absolute);

  /// <inheritdoc />
  public Uri CommentUrl(string owner, string name, int number, long commentId) =>
    new(string.Create(CultureInfo.InvariantCulture, $"{RepositoryOf(owner, name)}pull/{number}#issuecomment-{commentId}"), UriKind.Absolute);

  /// <inheritdoc />
  public Uri CommitUrl(string owner, string name, GitSha sha)
  {
    ArgumentNullException.ThrowIfNull(sha);

    return new Uri($"{RepositoryOf(owner, name)}commit/{sha.Value}", UriKind.Absolute);
  }

  #endregion
}
