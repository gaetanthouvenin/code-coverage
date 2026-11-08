// ------------------------------------------------------------------------------------------------
// <copyright file="WebHostTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeCoverage.Web.Configuration;
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
    html.ShouldContain("class=\"cc-topnav\"");
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

  // The Web never migrates: the Api does, and the Web waits for it (readiness probe).
  [Fact]
  public void Host_ShouldNotRegisterTheDatabaseMigrator()
  {
    var hostedServices = factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Name);

    hostedServices.ShouldNotContain("DatabaseMigrator");
  }

  #endregion
}
