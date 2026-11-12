// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigureGitHubOAuthOptions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using AspNet.Security.OAuth.GitHub;

using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.GitHub;

namespace CodeCoverage.Web.Configuration.Authentication;

/// <summary>
///   Gives the OAuth handler of GitHub the client identifier and secret of the App, read from the settings, and makes it
///   read them again after each change, without a restart.
/// </summary>
/// <remarks>
///   <para>
///     The handler keeps its options in <see cref="IOptionsMonitorCache{TOptions}" /> once built: a change of the
///     settings removes them, and the next request builds them again from the new values (ARCHITECTURE §4bis.1). The
///     options are those of <see cref="GitHubAuthenticationOptions" />, the type the handler reads, which derives from
///     <c>OAuthOptions</c>.
///   </para>
///   <para>
///     Every request asks each remote handler whether the request is its callback, which builds its options and checks
///     them: before the App is configured, an empty identifier would fail every page, the setup wizard included. The
///     handler then holds <see cref="Unconfigured" />, and the challenge endpoint refuses to start a sign-in.
///   </para>
/// </remarks>
internal sealed class ConfigureGitHubOAuthOptions : IConfigureNamedOptions<GitHubAuthenticationOptions>, IDisposable
{
  #region Fields

  /// <summary>The value the handler holds for a client identifier or secret that is not configured yet.</summary>
  public const string Unconfigured = "unconfigured";

  private readonly IOptionsMonitor<GitHubOptions> _github;

  private readonly ISecretProtector _protector;

  private readonly IDisposable? _registration;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ConfigureGitHubOAuthOptions" /> class.</summary>
  /// <param name="github">The settings of GitHub.</param>
  /// <param name="protector">The protector of the stored secrets.</param>
  /// <param name="cache">The cache of the options of the handler, emptied on each change of the settings.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public ConfigureGitHubOAuthOptions(
    IOptionsMonitor<GitHubOptions> github,
    ISecretProtector protector,
    IOptionsMonitorCache<GitHubAuthenticationOptions> cache)
  {
    ArgumentNullException.ThrowIfNull(github);
    ArgumentNullException.ThrowIfNull(protector);
    ArgumentNullException.ThrowIfNull(cache);

    _github = github;
    _protector = protector;
    _registration = github.OnChange(_ => cache.TryRemove(GitHubAuthenticationDefaults.AuthenticationScheme));
  }

  #endregion

  #region Implementation of IConfigureNamedOptions<GitHubAuthenticationOptions>

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
  public void Configure(string? name, GitHubAuthenticationOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    if (name != GitHubAuthenticationDefaults.AuthenticationScheme)
    {
      return;
    }

    var github = _github.CurrentValue;

    options.ClientId = string.IsNullOrWhiteSpace(github.ClientId) ? Unconfigured : github.ClientId;
    options.ClientSecret = string.IsNullOrWhiteSpace(github.ClientSecret) ? Unconfigured : github.GetClientSecret(_protector);
  }

  /// <inheritdoc />
  public void Configure(GitHubAuthenticationOptions options) => Configure(Options.DefaultName, options);

  #endregion

  #region Implementation of IDisposable

  /// <inheritdoc />
  public void Dispose() => _registration?.Dispose();

  #endregion
}
