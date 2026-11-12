// ------------------------------------------------------------------------------------------------
// <copyright file="AuthenticationEndpointsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Authentication;

[Collection(CodeCoverageWebCollection.Name)]
public sealed class AuthenticationEndpointsTests(CodeCoverageWebFactory factory)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_Challenge_ShouldRedirectToTheAuthorizationOfGitHub()
  {
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri("/signin/github?returnUrl=%2Fgithub%2Focto-org", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    var location = response.Headers.Location.ShouldNotBeNull().ToString();
    location.ShouldStartWith("https://github.com/login/oauth/authorize?");
    location.ShouldContain($"client_id={CodeCoverageWebFactory.ClientId}");
    location.ShouldContain("redirect_uri=http%3A%2F%2Flocalhost%2Fsignin-github");
    location.ShouldNotContain("scope=user");
  }

  [Fact]
  public async Task Post_SignOut_WithoutAntiforgeryToken_ShouldReturn400()
  {
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.PostAsync(new Uri("/signout", UriKind.Relative), new FormUrlEncodedContent([]), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Theory]
  [InlineData("en", "Sign in with GitHub")]
  [InlineData("fr", "Se connecter avec GitHub")]
  public async Task Get_SignIn_ShouldRenderTheLocalizedButton(string language, string label)
  {
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

    using var response = await client.GetAsync(new Uri("/signin", UriKind.Relative), Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    html.ShouldContain("class=\"cc-signin\"");
    html.ShouldContain(label);
    html.ShouldContain("href=\"signin/github?returnUrl=%2F\"");
  }

  // The test scheme stands for the session cookie: the page shows who is signed in, never starts a second sign-in.
  [Fact]
  public async Task Get_SignIn_WithTestSession_ShouldNotOfferASecondSignIn()
  {
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, "42");

    using var response = await client.GetAsync(new Uri("/signin", UriKind.Relative), Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    html.ShouldContain($"Signed in as {TestAuthenticationHandler.Login}.");
    html.ShouldContain("action=\"signout\"");
    html.ShouldNotContain("signin/github");
  }

  [Theory]
  [InlineData(null, "/")]
  [InlineData("", "/")]
  [InlineData("/", "/")]
  [InlineData("/github/octo-org/repo?tab=commits", "/github/octo-org/repo?tab=commits")]
  [InlineData("https://evil.example/", "/")]
  [InlineData("//evil.example/", "/")]
  [InlineData("/\\evil.example/", "/")]
  [InlineData("/path\\evil", "/")]
  [InlineData("relative", "/")]
  public void LocalOrHome_ShouldKeepLocalPathsOnly(string? returnUrl, string expected)
  {
    AuthenticationEndpoints.LocalOrHome(returnUrl).ShouldBe(expected);
  }

  #endregion
}
