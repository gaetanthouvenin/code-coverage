// ------------------------------------------------------------------------------------------------
// <copyright file="CultureEndpointsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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

  #endregion
}
