// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigureGitHubOAuthOptionsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using AspNet.Security.OAuth.GitHub;

using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Web.Configuration.Authentication;

namespace CodeCoverage.Web.Tests.Authentication;

public sealed class ConfigureGitHubOAuthOptionsTests : IDisposable
{
  #region Fields

  private readonly GitHubOptions _github = new() { ClientId = "Iv1.first", ClientSecret = "protected-secret" };

  private readonly IOptionsMonitor<GitHubOptions> _monitor = A.Fake<IOptionsMonitor<GitHubOptions>>();

  private readonly IOptionsMonitorCache<GitHubAuthenticationOptions> _cache = A.Fake<IOptionsMonitorCache<GitHubAuthenticationOptions>>();

  private readonly IDisposable _registration = A.Fake<IDisposable>();

  private readonly ConfigureGitHubOAuthOptions _configure;

  private Action<GitHubOptions, string?>? _listener;

  #endregion

  #region Ctors

  public ConfigureGitHubOAuthOptionsTests()
  {
    var protector = A.Fake<ISecretProtector>();

    A.CallTo(() => protector.Unprotect("protected-secret")).Returns("clear-secret");
    A.CallTo(() => _monitor.CurrentValue).ReturnsLazily(() => _github);
    A.CallTo(() => _monitor.OnChange(A<Action<GitHubOptions, string?>>._))
     .ReturnsLazily((Action<GitHubOptions, string?> listener) =>
     {
       _listener = listener;
       return _registration;
     });

    _configure = new ConfigureGitHubOAuthOptions(_monitor, protector, _cache);
  }

  #endregion

  #region Methods

  [Fact]
  public void ClientIdChange_ShouldInvalidateCache()
  {
    _github.ClientId = "Iv1.second";

    _listener.ShouldNotBeNull().Invoke(_github, null);

    A.CallTo(() => _cache.TryRemove(GitHubAuthenticationDefaults.AuthenticationScheme)).MustHaveHappenedOnceExactly();
    Configured().ClientId.ShouldBe("Iv1.second");
  }

  [Fact]
  public void Configure_ShouldCopyTheClientIdAndTheUnprotectedSecret()
  {
    var options = Configured();

    options.ClientId.ShouldBe("Iv1.first");
    options.ClientSecret.ShouldBe("clear-secret");
  }

  // Each request builds the options of the remote handlers: an empty identifier would fail every page.
  [Fact]
  public void Configure_AppNotConfigured_ShouldHoldThePlaceholder()
  {
    _github.ClientId = null;
    _github.ClientSecret = " ";

    var options = Configured();

    options.ClientId.ShouldBe(ConfigureGitHubOAuthOptions.Unconfigured);
    options.ClientSecret.ShouldBe(ConfigureGitHubOAuthOptions.Unconfigured);
  }

  [Fact]
  public void Configure_OtherScheme_ShouldChangeNothing()
  {
    var options = new GitHubAuthenticationOptions();

    _configure.Configure("Other", options);

    options.ClientId.ShouldBeNull();
  }

  [Fact]
  public void Dispose_ShouldStopFollowingTheSettings()
  {
    _configure.Dispose();

    A.CallTo(() => _registration.Dispose()).MustHaveHappenedOnceExactly();
  }

  public void Dispose() => _configure.Dispose();

  private GitHubAuthenticationOptions Configured()
  {
    var options = new GitHubAuthenticationOptions();

    _configure.Configure(GitHubAuthenticationDefaults.AuthenticationScheme, options);

    return options;
  }

  #endregion
}
