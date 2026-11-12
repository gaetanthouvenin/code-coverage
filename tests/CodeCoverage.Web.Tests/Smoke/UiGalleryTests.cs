// ------------------------------------------------------------------------------------------------
// <copyright file="UiGalleryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.AspNetCore.Hosting;

using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Smoke;

// The /_ui gallery of the UI components, served in Development only (PLAN M0.12a).
[Collection(CodeCoverageWebCollection.Name)]
public sealed class UiGalleryTests
{
  #region Fields

  private readonly CodeCoverageWebFactory _factory;

  #endregion

  #region Ctors

  public UiGalleryTests(CodeCoverageWebFactory factory)
  {
    _factory = factory;
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_UiGallery_InDevelopment_ShouldRenderTheComponentsInBothThemes()
  {
    using var client = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

    using var response = await client.GetAsync(new Uri("/_ui", UriKind.Relative), Ct);
    var html = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    html.ShouldContain("css/tokens");
    html.ShouldContain("css/components");
    html.ShouldContain("class=\"cc-topnav\"");
    html.ShouldContain("cc-kpi cc-kpi--accent");
    html.ShouldContain("role=\"tablist\"");
    html.ShouldContain("class=\"cc-chart\"");
    html.ShouldContain("class=\"cc-panel__foot\"");
    html.ShouldContain("class=\"cc-spark\"");
    html.ShouldContain("data-theme=\"light\"");
    html.ShouldContain("data-theme=\"dark\"");
  }

  [Fact]
  public async Task Get_UiGallery_InProduction_ShouldReturn404()
  {
    using var client = _factory.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();

    using var response = await client.GetAsync(new Uri("/_ui", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Theory]
  [InlineData("/css/tokens.css")]
  [InlineData("/css/components.css")]
  [InlineData("/js/cc-ui.js")]
  public async Task Get_UiAsset_ShouldBeServed(string path)
  {
    using var client = _factory.CreateClient();

    using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  #endregion
}
