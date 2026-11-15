// ------------------------------------------------------------------------------------------------
// <copyright file="WebHostTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Web.Configuration;
using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Smoke;

// The Blazor Server host: probes, security headers, the shell and the localized status pages
// (PLAN M0.12).
[Collection(CodeCoverageWebCollection.Name)]
public sealed class WebHostTests(CodeCoverageWebFactory factory)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_Live_ShouldReturn200()
  {
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(new Uri(HealthProbes.LivePath, UriKind.Relative), Ct);
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    document.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");
  }

  // The Web never migrates: without a migrated database it stays not ready.
  [Fact]
  public async Task Get_Ready_WithUnreachableDatabase_ShouldReturn503()
  {
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(new Uri(HealthProbes.ReadyPath, UriKind.Relative), Ct);
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    document.RootElement.GetProperty("status").GetString().ShouldBe("Unhealthy");
    document.RootElement.GetProperty("checks").GetProperty("migrations").GetString().ShouldBe("Unhealthy");
  }

  [Fact]
  public async Task Get_Home_ShouldRenderTheShellWithTheSecurityHeaders()
  {
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    html.ShouldContain("class=\"cc-shell\"");
    // Signed out: the public bar (M4.02o), whose documentation icon gives way to the mobile menu up to 640px.
    html.ShouldContain("class=\"cc-topnav cc-topnav__public\"");
    html.ShouldContain("css/app");
    html.ShouldContain("class=\"cc-header__title\"");
    response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    response.Headers.GetValues("Content-Security-Policy").ShouldBe(["frame-ancestors 'none'"]);
    response.Headers.Contains("X-Frame-Options").ShouldBeFalse();
  }

  [Theory]
  [InlineData("en", "Page not found")]
  [InlineData("fr", "Page introuvable")]
  public async Task Get_UnknownPage_ShouldReturn404WithTheLocalizedNotFoundPage(string language, string title)
  {
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));

    using var response = await client.GetAsync(new Uri("/no/such/page/here", UriKind.Relative), Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    html.ShouldContain($"lang=\"{language}\"");
    html.ShouldContain(title);
    html.ShouldContain("class=\"cc-shell\"");
  }

  // M4.02ab: the bar of a signed-in user is read from the fakes of the factory, never from the unreachable database, whose
  // refusal may come before or after the first render and lead to the page or to the sign-in.
  [Fact]
  public async Task Get_UnknownPage_SignedIn_ShouldShowTheMenuOfTheUser()
  {
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, CodeCoverageWebFactory.KnownUserId.ToString(CultureInfo.InvariantCulture));

    using var response = await client.GetAsync(new Uri("/no/such/page/here", UriKind.Relative), Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    html.ShouldContain($"<span class=\"cc-user-menu__name\">{TestAuthenticationHandler.Login}</span>");
  }

  // M4.02s: a regional variant of the browser falls back on its language; a language the console does not speak, on English.
  [Theory]
  [InlineData("it-CH", "it", "Pagina non trovata")]
  [InlineData("es-MX", "es", "Página no encontrada")]
  [InlineData("de", "en", "Page not found")]
  public async Task Get_WithTheLanguageOfTheBrowser_ShouldServeTheConsoleInItsLanguage(string browser, string language, string title)
  {
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(browser));

    using var response = await client.GetAsync(new Uri("/no/such/page/here", UriKind.Relative), Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    html.ShouldContain($"<html lang=\"{language}\"");
    html.ShouldContain(title);
  }

  [Theory]
  [InlineData("/error", "en", "An error occurred")]
  [InlineData("/error", "fr", "Une erreur est survenue")]
  [InlineData("/access-denied", "en", "Access denied")]
  [InlineData("/access-denied", "fr", "Accès refusé")]
  public async Task Get_StatusPage_ShouldBeLocalized(string path, string language, string title)
  {
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));

    using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    html.ShouldContain(title);
  }

  // M4.02m: only a 404 is « not found »; any other status answered without a body is an error, whatever the method.
  [Theory]
  [InlineData("POST", "/signout", HttpStatusCode.BadRequest)]
  [InlineData("PUT", "/", HttpStatusCode.MethodNotAllowed)]
  public async Task Send_StatusWithoutBody_ShouldShowTheErrorPageRatherThanNotFound(string method, string path, HttpStatusCode status)
  {
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr"));

    using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative)) { Content = new FormUrlEncodedContent([]) };
    using var response = await client.SendAsync(request, Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(status);
    html.ShouldContain("Une erreur est survenue");
    html.ShouldNotContain("introuvable");
  }

  // M4.02m: with the database down, a signed-in user gets the error page, never a bare 500.
  [Theory]
  [InlineData("/", HttpStatusCode.InternalServerError)]
  [InlineData("/error", HttpStatusCode.OK)]
  public async Task Get_WithASessionAndTheDatabaseDown_ShouldShowTheErrorPage(string path, HttpStatusCode status)
  {
    var users = A.Fake<IUserRepository>();
    A.CallTo(() => users.FindAsync(A<long>._, A<CancellationToken>._)).ThrowsAsync(new InvalidOperationException("The database is down."));
    await using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => users)));
    using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
    request.Headers.Add("Cookie", $"{AuthenticationConfiguration.SessionCookieName}={SessionCookie(host, CodeCoverageWebFactory.KnownUserId)}");
    request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr"));

    using var response = await client.SendAsync(request, Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(status);
    html.ShouldContain("Une erreur est survenue");
  }

  // M4.02ac: a page that fails after the session was validated re-executes /error, which shows the error page instead
  // of sending the user to the sign-in page.
  [Fact]
  public async Task Get_WithASessionAndAPageThatFails_ShouldShowTheErrorPage()
  {
    var user = AppUser.Create(Forge.GitHub, "583231", TestAuthenticationHandler.Login, null);
    typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(user, CodeCoverageWebFactory.KnownUserId);
    // A session stands only for a user whose tokens are kept.
    user.ReplaceTokens(A.Fake<ISecretProtector>(), "ghu_token", null, null, null);
    var users = A.Fake<IUserRepository>();
    A.CallTo(() => users.FindAsync(CodeCoverageWebFactory.KnownUserId, A<CancellationToken>._)).Returns(user);
    // The bar of the page fails before the response starts: the exception handler re-executes /error.
    var memberships = A.Fake<IOrganizationMembershipRepository>();
    A.CallTo(() => memberships.ListOfUserAsync(A<long>._, A<CancellationToken>._)).Throws(new InvalidOperationException("The page fails."));
    await using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => users).AddScoped(_ => memberships)));
    using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/no/such/page/here", UriKind.Relative));
    request.Headers.Add("Cookie", $"{AuthenticationConfiguration.SessionCookieName}={SessionCookie(host, CodeCoverageWebFactory.KnownUserId)}");
    request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr"));

    using var response = await client.SendAsync(request, Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    response.Headers.Location.ShouldBeNull();
    html.ShouldContain("Une erreur est survenue");
  }

  // The Web never migrates: the Api does, and the Web waits for it (readiness probe).
  [Fact]
  public void Host_ShouldNotRegisterTheDatabaseMigrator()
  {
    var hostedServices = factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Name);

    hostedServices.ShouldNotContain("DatabaseMigrator");
  }

  private static string SessionCookie(WebApplicationFactory<Program> host, long userId)
  {
    var options = host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
    var ticket = new AuthenticationTicket(SessionPrincipal.Create(userId, DateTimeOffset.UtcNow), CookieAuthenticationDefaults.AuthenticationScheme);

    return options.TicketDataFormat.Protect(ticket);
  }

  #endregion
}
