// ------------------------------------------------------------------------------------------------
// <copyright file="DocsHostTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using CodeCoverage.Web.Documentation;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Documentation;

// /docs served by the host (M4.02o): every published document, nothing else, without sign-in and before the setup.
[Collection(CodeCoverageWebCollection.Name)]
public sealed class DocsHostTests
{
  #region Fields

  private readonly CodeCoverageWebFactory _factory;

  #endregion

  #region Ctors

  public DocsHostTests(CodeCoverageWebFactory factory)
  {
    _factory = factory;
  }

  #endregion

  #region Properties

  public static TheoryData<string> Slugs => [.. DocumentationCatalog.Entries.Select(static e => e.Slug)];

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [MemberData(nameof(Slugs))]
  public async Task Get_PublishedDocument_ShouldServeItWithoutSignIn(string slug)
  {
    using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri($"/docs/{slug}", UriKind.Relative), Ct);
    var html = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    html.ShouldContain("class=\"cc-docs__title\"");
    html.ShouldContain("class=\"cc-md\"");
    html.ShouldContain(DocumentationCatalog.Find(slug)!.SourcePath);
  }

  [Theory]
  [InlineData("/docs/plan")]
  [InlineData("/docs/PLAN.md")]
  [InlineData("/docs/architecture")]
  [InlineData("/docs/conventions")]
  [InlineData("/docs/adr")]
  public async Task Get_DocumentOutsideTheWhitelist_ShouldAnswer404(string path)
  {
    using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
    var html = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    html.ShouldNotContain("class=\"cc-md\"");
  }

  [Fact]
  public async Task Get_Docs_ShouldOpenTheFirstPage()
  {
    using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var response = await client.GetAsync(new Uri("/docs", UriKind.Relative), Ct);
    var html = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    html.ShouldContain(DocumentationCatalog.Default.SourcePath);
  }

  // Before the GitHub App exists, every page leads to the wizard, except the documentation that explains it.
  [Fact]
  public async Task Get_Docs_BeforeTheSetup_ShouldNotLeadToTheWizard()
  {
    using var host = _factory.WithWebHostBuilder(b => b.UseSetting("GitHub:AppId", string.Empty));
    using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    using var home = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);
    using var docs = await client.GetAsync(new Uri("/docs/operations", UriKind.Relative), Ct);

    home.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    home.Headers.Location!.OriginalString.ShouldBe("/setup");
    docs.StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  #endregion
}
