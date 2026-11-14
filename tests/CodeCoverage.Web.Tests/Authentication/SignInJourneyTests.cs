// ------------------------------------------------------------------------------------------------
// <copyright file="SignInJourneyTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Authentication;

// M4.02j: a deep link followed without a session leads to the sign-in, and the sign-in back to the page.
[Collection(CodeCoverageWebCollection.Name)]
public sealed class SignInJourneyTests(CodeCoverageWebFactory factory)
{
  #region Fields

  private const string PrivateRepository = "/github/octo-org/app/commits?branch=main";

  private const string ExpectedReturnUrl = "returnUrl=%2Fgithub%2Focto-org%2Fapp%2Fcommits%3Fbranch%3Dmain";

  // A user the host does not know: its session cookie is rejected.
  private const long RemovedUserId = 999;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_PrivateRepository_WhileAnonymous_ShouldRedirectToTheSignInWithoutExpired()
  {
    await using var host = WithoutOrganizations();
    using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri(PrivateRepository, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    var location = response.Headers.Location.ShouldNotBeNull().ToString();
    location.ShouldEndWith($"/signin?{ExpectedReturnUrl}");
    location.ShouldNotContain("expired");
  }

  [Fact]
  public async Task Get_PrivateRepository_WithARejectedCookie_ShouldRedirectToTheSignInWithExpired()
  {
    await using var host = WithoutOrganizations();
    using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    using var request = new HttpRequestMessage(HttpMethod.Get, PrivateRepository);
    request.Headers.Add("Cookie", $"{AuthenticationConfiguration.SessionCookieName}={SessionCookie(host, RemovedUserId)}");

    using var response = await client.SendAsync(request, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.Headers.Location.ShouldNotBeNull().ToString().ShouldEndWith($"/signin?{ExpectedReturnUrl}&expired=true");
  }

  [Fact]
  public async Task Get_SignIn_WithALocalReturnUrl_ShouldStartTheSignInBackToIt()
  {
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(new Uri($"/signin?{ExpectedReturnUrl}", UriKind.Relative), Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    html.ShouldContain($"href=\"signin/github?{ExpectedReturnUrl}\"");
    html.ShouldNotContain("cc-banner", Case.Sensitive, "a visitor who never signed in is not told a session expired");
  }

  [Theory]
  [InlineData("https%3A%2F%2Fevil.example%2F")]
  [InlineData("%2F%2Fevil.example%2F")]
  [InlineData("%2F%5Cevil.example%2F")]
  public async Task Get_SignIn_WithAnExternalReturnUrl_ShouldGoBackHomeInstead(string returnUrl)
  {
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(new Uri($"/signin?returnUrl={returnUrl}", UriKind.Relative), Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    html.ShouldContain("href=\"signin/github?returnUrl=%2F\"");
    html.ShouldNotContain("evil.example");
  }

  // The challenge keeps a local return address only: an external one leads home after the sign-in.
  [Theory]
  [InlineData("%2Fgithub%2Focto-org%2Fapp", "/github/octo-org/app")]
  [InlineData("https%3A%2F%2Fevil.example%2F", "/")]
  public async Task Get_Challenge_ShouldKeepALocalReturnUrlOnly(string returnUrl, string expected)
  {
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri($"/signin/github?returnUrl={returnUrl}", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    var location = response.Headers.Location.ShouldNotBeNull();
    var state = System.Web.HttpUtility.ParseQueryString(location.Query)["state"].ShouldNotBeNull();
    var options = factory.Services.GetRequiredService<IOptionsMonitor<AspNet.Security.OAuth.GitHub.GitHubAuthenticationOptions>>()
                         .Get(AspNet.Security.OAuth.GitHub.GitHubAuthenticationDefaults.AuthenticationScheme);
    options.StateDataFormat.Unprotect(state).ShouldNotBeNull().RedirectUri.ShouldBe(expected);
  }

  private static string SessionCookie(WebApplicationFactory<Program> host, long userId)
  {
    var options = host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
    var ticket = new AuthenticationTicket(SessionPrincipal.Create(userId, DateTimeOffset.UtcNow), CookieAuthenticationDefaults.AuthenticationScheme);

    return options.TicketDataFormat.Protect(ticket);
  }

  // No organization is known: every repository address is refused as missing, without the unreachable database.
  private WebApplicationFactory<Program> WithoutOrganizations()
  {
    var directory = A.Fake<IOrganizationDirectory>();
    A.CallTo(() => directory.FindByLoginAsync(A<Forge>._, A<string>._, A<CancellationToken>._)).Returns((Organization?)null);

    return factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => directory)));
  }

  #endregion
}
