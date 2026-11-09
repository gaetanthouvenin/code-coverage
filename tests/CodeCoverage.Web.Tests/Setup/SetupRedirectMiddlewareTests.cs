// ------------------------------------------------------------------------------------------------
// <copyright file="SetupRedirectMiddlewareTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Web.Configuration.Setup;

namespace CodeCoverage.Web.Tests.Setup;

public sealed class SetupRedirectMiddlewareTests
{
  #region Fields

  private readonly ISetupTokenIssuer _issuer = A.Fake<ISetupTokenIssuer>();

  private bool _nextCalled;

  #endregion

  #region Methods

  [Fact]
  public async Task Invoke_AppNotCreated_ShouldRedirectAPageToTheWizardWithoutTheToken()
  {
    var context = await InvokeAsync(appId: null, "/");

    context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
    context.Response.Headers.Location.ToString().ShouldBe("/setup");
    _nextCalled.ShouldBeFalse();
    A.CallTo(() => _issuer.EnsureIssued()).MustHaveHappenedOnceExactly();
  }

  // GitHub__AppId in the environment reaches the configuration: the wizard is optional.
  [Theory]
  [InlineData("/")]
  [InlineData("/setup")]
  public async Task Invoke_AppCreated_ShouldLetEverythingThroughAndIssueNothing(string path)
  {
    var context = await InvokeAsync(appId: "12345", path);

    context.Response.Headers.Location.ToString().ShouldBeEmpty();
    _nextCalled.ShouldBeTrue();
    A.CallTo(() => _issuer.EnsureIssued()).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("/setup")]
  [InlineData("/SETUP")]
  [InlineData("/health/live")]
  [InlineData("/health/ready")]
  [InlineData("/error")]
  [InlineData("/not-found")]
  [InlineData("/_framework/blazor.web.js")]
  [InlineData("/_blazor/negotiate")]
  [InlineData("/app.css")]
  public async Task Invoke_OpenPath_ShouldPassThroughAndStillIssueTheToken(string path)
  {
    await InvokeAsync(appId: null, path);

    _nextCalled.ShouldBeTrue();
    A.CallTo(() => _issuer.EnsureIssued()).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Invoke_PathThatOnlyStartsLikeTheWizard_ShouldRedirect()
  {
    await InvokeAsync(appId: null, "/setupx");

    _nextCalled.ShouldBeFalse();
  }

  [Fact]
  public async Task Invoke_NotANavigation_ShouldPassThrough()
  {
    await InvokeAsync(appId: null, "/", HttpMethods.Post);

    _nextCalled.ShouldBeTrue();
  }

  [Fact]
  public async Task Invoke_EmptyAppId_ShouldCountAsNotCreated()
  {
    await InvokeAsync(appId: string.Empty, "/");

    _nextCalled.ShouldBeFalse();
  }

  private async Task<DefaultHttpContext> InvokeAsync(string? appId, string path, string method = "GET")
  {
    var configuration = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?> { [SettingDefinitions.GitHubAppId] = appId })
                        .Build();

    var middleware = new SetupRedirectMiddleware(
      _ =>
      {
        _nextCalled = true;

        return Task.CompletedTask;
      },
      configuration,
      _issuer
    );

    var context = new DefaultHttpContext();
    context.Request.Path = path;
    context.Request.Method = method;

    await middleware.InvokeAsync(context);

    return context;
  }

  #endregion
}
