// ------------------------------------------------------------------------------------------------
// <copyright file="SetupTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Commands.CompleteGitHubAppManifest;
using CodeCoverage.Application.Features.Settings.Commands.CompleteSetup;
using CodeCoverage.Application.Features.Settings.Commands.SaveGitHubApp;
using CodeCoverage.Application.Features.Settings.Commands.SavePublicUrls;
using CodeCoverage.Application.Features.Settings.Queries.GetSetupState;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Web.Components.Pages;

using SetupPage = CodeCoverage.Web.Components.Pages.Setup;
using CodeCoverage.Web.Configuration.Setup;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// The first-start wizard (M3.43), rendered on the server alone, against a fake sender.
public sealed class SetupTests : UiTestContext
{
  #region Fields

  private const string Token = "setup-token";

  private const string Pem = "-----BEGIN RSA PRIVATE KEY-----\nMIIE\n-----END RSA PRIVATE KEY-----";

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly SetupCookie _cookie = new(new EphemeralDataProtectionProvider());

  private readonly DefaultHttpContext _context = new();

  private SetupState _state = new(false, false, false);

  #endregion

  #region Ctors

  public SetupTests()
  {
    Services.AddFakeSenderRunner(_sender);
    Services.AddSingleton(_cookie);

    A.CallTo(() => _sender.Send(A<GetSetupStateQuery>._, A<CancellationToken>._)).ReturnsLazily(() => new ValueTask<SetupState>(_state));
  }

  #endregion

  #region Methods

  // Without the cookie, the wizard says the token is required: it never sends anything, and never sends the visitor
  // to the sign-in, which would lead back here.
  [Fact]
  public void Render_WithoutToken_ShouldAskForIt()
  {
    var cut = Render<SetupPage>(WithContext());

    cut.Find("[data-state=invalidtoken]").TextContent.ShouldContain(L("Setup_InvalidTokenTitle"));
    A.CallTo(() => _sender.Send(A<GetSetupStateQuery>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // A threat case: a token the instance no longer knows is refused, and the cookie that held it is dropped.
  [Fact]
  public void Render_RefusedToken_ShouldAskForItAgainAndDropTheCookie()
  {
    A.CallTo(() => _sender.Send(A<GetSetupStateQuery>._, A<CancellationToken>._))
     .ThrowsAsync(new UnauthorizedException(SettingErrors.InvalidSetupToken, "Invalid."));
    WithToken();

    var cut = Render<SetupPage>(WithContext());

    cut.Find("[data-state=invalidtoken]").ShouldNotBeNull();
    _context.Response.Headers.SetCookie.ToString().ShouldContain(SetupCookie.Name);
    Navigation.Uri.ShouldNotContain("signin");
  }

  [Fact]
  public void Render_WithToken_ShouldShowTheSteps()
  {
    WithToken();

    var cut = Render<SetupPage>(WithContext());

    cut.FindAll(".cc-stepper li").Count.ShouldBe(3);
    cut.Find("[data-panel=urls]").ShouldNotBeNull();
    cut.FindAll("[data-panel=admins]").ShouldBeEmpty();
  }

  [Fact]
  public void Render_AppCreated_ShouldCompleteWithoutInstallation()
  {
    _state = new SetupState(true, true, false);
    WithToken();

    var cut = Render<SetupPage>(WithContext());

    cut.Find("[data-panel=admins]").ShouldNotBeNull();
    cut.FindAll("[data-panel=app]").ShouldBeEmpty();
    cut.Markup.ShouldNotContain("install", Case.Insensitive);
  }

  [Fact]
  public void Render_Complete_ShouldSayTheInstanceIsReady()
  {
    _state = new SetupState(true, true, true);
    WithToken();

    var cut = Render<SetupPage>(WithContext());

    cut.Find("[data-state=complete]").TextContent.ShouldContain(L("Setup_CompleteTitle"));
  }

  [Fact]
  public void SaveUrls_ShouldSendTheAddresses()
  {
    WithToken();
    var cut = Render<SetupPage>(WithContext());

    var inputs = cut.FindAll("[data-panel=urls] input");
    inputs[0].Input("https://coverage.example.com");
    inputs[1].Input("https://coverage-api.example.com");
    cut.Find("[data-panel=urls] form").Submit();

    A.CallTo(() => _sender.Send(
                A<SavePublicUrlsCommand>.That.Matches(c => c.SetupToken == Token && c.WebPublicUrl == "https://coverage.example.com"),
                A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public void SaveApp_ShouldSendWhatWasTyped()
  {
    _state = new SetupState(false, true, false, "https://coverage.example.com", "https://coverage-api.example.com");
    WithToken();
    var cut = Render<SetupPage>(WithContext());

    var inputs = cut.FindAll("[data-panel=manual] input");
    inputs[0].Input("12345");
    inputs[1].Input("Iv1.client");
    inputs[2].Input("client-secret");
    inputs[3].Input("webhook-secret");
    inputs[4].Input(Pem);
    cut.Find("[data-panel=manual] form").Submit();

    A.CallTo(() => _sender.Send(
                A<SaveGitHubAppCommand>.That.Matches(c => c.SetupToken == Token && c.AppId == "12345" && c.PrivateKeyPem == Pem),
                A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public void Complete_ShouldNameTheAdministrators()
  {
    _state = new SetupState(true, true, false);
    WithToken();
    var cut = Render<SetupPage>(WithContext());

    cut.Find("[data-panel=admins] input").Input("583231");
    cut.Find("[data-panel=admins] form").Submit();

    A.CallTo(() => _sender.Send(A<CompleteSetupCommand>.That.Matches(c => c.AdminUserIds == "583231"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // The manifest is prepared here, with the state kept in the cookie and carried to GitHub.
  [Fact]
  public void StartManifest_ShouldPostTheManifestWithAState()
  {
    _state = new SetupState(false, true, false, "https://coverage.example.com", "https://coverage-api.example.com");
    WithToken();
    var cut = Render<SetupPage>(WithContext());

    var inputs = cut.FindAll("[data-panel=app] form input");
    inputs[0].Input("CodeCoverage");
    cut.Find("[data-panel=app] form").Submit();

    var form = cut.Find("[data-form=manifest]");
    form.GetAttribute("action")!.ShouldStartWith("https://github.com/settings/apps/new?state=");
    form.QuerySelector("input[name=manifest]")!.GetAttribute("value")!.ShouldContain("\"public\":true");
    _context.Response.Headers.SetCookie.ToString().ShouldContain(SetupCookie.Name);
  }

  // A threat case: a return whose state is not the one this browser started is refused, and nothing is stored.
  [Fact]
  public void Return_WithAnotherState_ShouldRefuseTheCode()
  {
    WithToken("the-state");
    Navigation.NavigateTo("setup?code=abc&state=replayed");

    var cut = Render<SetupPage>(WithContext());

    cut.Find("[data-state=error]").TextContent.ShouldContain(L("Setup_InvalidState"));
    A.CallTo(() => _sender.Send(A<CompleteGitHubAppManifestCommand>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // A threat case: a return with no state at all (a cookie that never started one) is refused the same way.
  [Fact]
  public void Return_WithoutStateInTheCookie_ShouldRefuseTheCode()
  {
    WithToken();
    Navigation.NavigateTo("setup?code=abc&state=replayed");

    var cut = Render<SetupPage>(WithContext());

    cut.Find("[data-state=error]").ShouldNotBeNull();
    A.CallTo(() => _sender.Send(A<CompleteGitHubAppManifestCommand>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public void Return_WithTheStateOfTheCookie_ShouldStoreTheApp()
  {
    WithToken("the-state");
    Navigation.NavigateTo("setup?code=abc&state=the-state");

    Render<SetupPage>(WithContext());

    A.CallTo(() => _sender.Send(A<CompleteGitHubAppManifestCommand>.That.Matches(c => c.Code == "abc" && c.SetupToken == Token), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // The App of an instance already set up is never replaced: the refusal is shown, in its own words.
  [Fact]
  public void SaveApp_AlreadyCreated_ShouldShowTheRefusal()
  {
    _state = new SetupState(false, true, false, "https://coverage.example.com", "https://coverage-api.example.com");
    A.CallTo(() => _sender.Send(A<SaveGitHubAppCommand>._, A<CancellationToken>._))
     .ThrowsAsync(new ConflictException(SettingErrors.GitHubAppAlreadyCreated, "Already created."));
    WithToken();
    var cut = Render<SetupPage>(WithContext());

    var inputs = cut.FindAll("[data-panel=manual] input");
    inputs[0].Input("12345");
    inputs[1].Input("Iv1.client");
    inputs[2].Input("client-secret");
    inputs[3].Input("webhook-secret");
    inputs[4].Input(Pem);
    cut.Find("[data-panel=manual] form").Submit();

    cut.Find("[data-state=error]").TextContent.ShouldContain(L(SettingErrors.GitHubAppAlreadyCreated));
  }

  // The cookie the wizard reads is the one its own writer produced, as a browser sends it back.
  private void WithToken(string? state = null)
  {
    var writer = new DefaultHttpContext();
    _cookie.Write(writer, Token, state);

    var header = writer.Response.Headers.SetCookie.ToString();
    var value = header[(header.IndexOf('=', StringComparison.Ordinal) + 1)..header.IndexOf(';', StringComparison.Ordinal)];

    _context.Request.Headers.Cookie = $"{SetupCookie.Name}={value}";
  }

  private Action<Bunit.ComponentParameterCollectionBuilder<SetupPage>> WithContext() =>
    p => p.AddCascadingValue(_context);

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
