// ------------------------------------------------------------------------------------------------
// <copyright file="CultureEndpointsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Web.Configuration.Authentication;

using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Configuration.Localization;

// The address of the language picker (M4.02t): it remembers a language of the console in the culture cookie, and only
// ever comes back to a page of the console.
[Collection(CodeCoverageWebCollection.Name)]
public sealed class CultureEndpointsTests(CodeCoverageWebFactory factory)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_Culture_WithALanguageOfTheConsole_ShouldSetTheCookieAndComeBack()
  {
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri("/culture?lang=it&returnUrl=%2Fdocs", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.Headers.Location!.OriginalString.ShouldBe("/docs");
    var cookie = response.Headers.GetValues("Set-Cookie").ShouldHaveSingleItem();
    cookie.ShouldStartWith(".AspNetCore.Culture=c%3Dit%7Cuic%3Dit;");
    cookie.ShouldContain("max-age=31536000", Case.Insensitive);
    cookie.ShouldContain("path=/", Case.Insensitive);
    cookie.ShouldContain("secure", Case.Insensitive);
    cookie.ShouldContain("samesite=lax", Case.Insensitive);
    cookie.ShouldContain("httponly", Case.Insensitive);
  }

  [Fact]
  public async Task Get_Culture_WithALanguageTheConsoleDoesNotSpeak_ShouldSetNothing()
  {
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri("/culture?lang=de&returnUrl=%2Fdocs", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.Headers.Location!.OriginalString.ShouldBe("/docs");
    response.Headers.Contains("Set-Cookie").ShouldBeFalse();
  }

  [Theory]
  [InlineData("https%3A%2F%2Fevil.example%2F")]
  [InlineData("%2F%2Fevil.example")]
  [InlineData("%2F%5Cevil.example")]
  [InlineData("")]
  public async Task Get_Culture_WithAReturnAddressOutsideTheConsole_ShouldGoHome(string returnUrl)
  {
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri($"/culture?lang=es&returnUrl={returnUrl}", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.Headers.Location!.OriginalString.ShouldBe("/");
  }

  // Before the GitHub App exists, the wizard still lets the language be chosen.
  [Fact]
  public async Task Get_Culture_BeforeTheSetup_ShouldNotLeadToTheWizard()
  {
    using var host = factory.WithWebHostBuilder(b => b.UseSetting("GitHub:AppId", string.Empty));
    using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri("/culture?lang=fr&returnUrl=%2Fsetup", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.Headers.Location!.OriginalString.ShouldBe("/setup");
    response.Headers.Contains("Set-Cookie").ShouldBeTrue();
  }

  // The cookie wins over the language of the browser.
  [Fact]
  public async Task Get_Page_WithTheCultureCookie_ShouldBeServedInItsLanguage()
  {
    using var client = factory.CreateClient();
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/no/such/page/here", UriKind.Relative));
    request.Headers.Add("Cookie", ".AspNetCore.Culture=c%3Des%7Cuic%3Des");
    request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr"));

    using var response = await client.SendAsync(request, Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    html.ShouldContain("<html lang=\"es\"");
    html.ShouldContain("Página no encontrada");
  }

  // M4.02u: signed in, the choice is kept by the account, and a browser without the culture cookie comes back to it.
  [Fact]
  public async Task Get_Culture_SignedIn_ShouldKeepTheLanguageInTheAccountForTheNextBrowser()
  {
    var user = AppUser.Create(Forge.GitHub, "583231", TestAuthenticationHandler.Login, null);
    typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(user, CodeCoverageWebFactory.KnownUserId);
    // A session stands only for a user whose tokens are kept.
    user.ReplaceTokens(A.Fake<ISecretProtector>(), "ghu_token", null, null, null);
    var users = A.Fake<IUserRepository>();
    A.CallTo(() => users.FindAsync(CodeCoverageWebFactory.KnownUserId, A<CancellationToken>._)).Returns(user);
    await using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => users)));
    using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    var session = $"{AuthenticationConfiguration.SessionCookieName}={SessionCookie(host, CodeCoverageWebFactory.KnownUserId)}";

    using var choose = new HttpRequestMessage(HttpMethod.Get, new Uri("/culture?lang=it&returnUrl=%2Fdocs", UriKind.Relative));
    choose.Headers.Add("Cookie", session);
    using var chosen = await client.SendAsync(choose, Ct);

    chosen.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    user.PreferredLanguage.ShouldBe("it");
    A.CallTo(() => users.UpdateAsync(user, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

    using var other = new HttpRequestMessage(HttpMethod.Get, new Uri("/no/such/page/here", UriKind.Relative));
    other.Headers.Add("Cookie", session);
    other.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr"));
    using var response = await client.SendAsync(other, Ct);
    var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    html.ShouldContain("<html lang=\"it\"");
    response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith(".AspNetCore.Culture=c%3Dit%7Cuic%3Dit;", StringComparison.Ordinal));
  }

  // Signed out, only the cookie remembers the language: nothing is sent to an account.
  [Fact]
  public async Task Get_Culture_SignedOut_ShouldOnlySetTheCookie()
  {
    var users = A.Fake<IUserRepository>();
    await using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => users)));
    using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri("/culture?lang=es&returnUrl=%2F", UriKind.Relative), Ct);

    response.Headers.Contains("Set-Cookie").ShouldBeTrue();
    A.CallTo(() => users.UpdateAsync(A<AppUser>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private static string SessionCookie(WebApplicationFactory<Program> host, long userId)
  {
    var options = host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
    var ticket = new AuthenticationTicket(SessionPrincipal.Create(userId, DateTimeOffset.UtcNow), CookieAuthenticationDefaults.AuthenticationScheme);

    return options.TicketDataFormat.Protect(ticket);
  }

  #endregion
}
