// ------------------------------------------------------------------------------------------------
// <copyright file="SetupRedirectMiddlewareTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.DataProtection;
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

  // GitHub__AppId in the environment reaches the configuration: the wizard is optional, nothing is redirected.
  [Theory]
  [InlineData("/")]
  [InlineData("/setup")]
  public async Task Invoke_AppCreated_ShouldLetEverythingThrough(string path)
  {
    var context = await InvokeAsync(appId: "12345", path);

    context.Response.Headers.Location.ToString().ShouldBeEmpty();
    _nextCalled.ShouldBeTrue();
  }

  // Decision 4: a token stays available while the setup is unfinished, so a restart in the middle never leaves the
  // operator without one.
  [Fact]
  public async Task Invoke_AppCreatedWithoutTheRest_ShouldStillIssueAToken()
  {
    await InvokeAsync(appId: "12345", "/");

    A.CallTo(() => _issuer.EnsureIssued()).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Invoke_SetupComplete_ShouldIssueNothing()
  {
    await InvokeAsync(appId: "12345", "/", complete: true);

    _nextCalled.ShouldBeTrue();
    A.CallTo(() => _issuer.EnsureIssued()).MustNotHaveHappened();
  }

  // The token given in the address moves to the protected cookie, and the address loses it at once.
  [Fact]
  public async Task Invoke_TokenInTheAddress_ShouldMoveItToTheCookie()
  {
    A.CallTo(() => _issuer.IsValid("secret")).Returns(true);

    var context = await InvokeAsync(appId: null, "/setup", query: "?token=secret");

    context.Response.Headers.Location.ToString().ShouldBe("/setup");
    context.Response.Headers.SetCookie.ToString().ShouldContain(SetupCookie.Name);
    context.Response.Headers.SetCookie.ToString().ShouldNotContain("secret");
    _nextCalled.ShouldBeFalse();
  }

  // A token that is not the one of the instance is dropped: no cookie, and the wizard shows its own state.
  [Fact]
  public async Task Invoke_WrongTokenInTheAddress_ShouldWriteNoCookie()
  {
    A.CallTo(() => _issuer.IsValid(A<string>._)).Returns(false);

    var context = await InvokeAsync(appId: null, "/setup", query: "?token=wrong");

    context.Response.Headers.Location.ToString().ShouldBe("/setup");
    context.Response.Headers.SetCookie.ToString().ShouldBeEmpty();
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

  private async Task<DefaultHttpContext> InvokeAsync(string? appId, string path, string method = "GET", string? query = null, bool complete = false)
  {
    var settings = new Dictionary<string, string?> { [SettingDefinitions.GitHubAppId] = appId };

    if (complete)
    {
      settings[SettingDefinitions.UrlsWebPublicUrl] = "https://coverage.example.com";
      settings[SettingDefinitions.UrlsApiPublicUrl] = "https://coverage-api.example.com";
      settings[SettingDefinitions.AccessAdminUserIds] = "583231";
    }

    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    var middleware = new SetupRedirectMiddleware(
      _ =>
      {
        _nextCalled = true;

        return Task.CompletedTask;
      },
      configuration,
      _issuer,
      new SetupCookie(new EphemeralDataProtectionProvider())
    );

    var context = new DefaultHttpContext();
    context.Request.Path = path;
    context.Request.Method = method;
    context.Request.QueryString = new QueryString(query ?? string.Empty);

    await middleware.InvokeAsync(context);

    return context;
  }

  #endregion
}
