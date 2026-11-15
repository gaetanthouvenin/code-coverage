// ------------------------------------------------------------------------------------------------
// <copyright file="DocsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Documentation;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// The in-app documentation (M4.02o, mockup #docs): the navigation, the contents, the pager and the unknown page.
public sealed class DocsTests : UiTestContext
{
  #region Fields

  private readonly DefaultHttpContext _context = new();

  #endregion

  #region Ctors

  public DocsTests()
  {
    Services.AddSingleton<DocumentationLibrary>();
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_WithoutPage_ShouldOpenTheFirstPageWithTheWholeNavigation()
  {
    var cut = Render<Docs>(WithContext());

    cut.WaitForAssertion(() => cut.Find("h1.cc-docs__title").TextContent.ShouldBe("Operations"));
    cut.FindAll(".cc-docs__link").Select(static a => a.GetAttribute("href"))
       .ShouldBe(DocumentationCatalog.Entries.Select(static e => e.Href));
    cut.Find(".cc-docs__link[aria-current=page]").GetAttribute("href").ShouldBe("docs/operations");
    cut.FindAll(".cc-docs__section h2").Select(static h => h.TextContent)
       .ShouldBe(DocumentationCatalog.Entries.Select(static e => e.Section).Distinct().Select(s => L($"Docs_Section_{s}")));
    cut.Find(".cc-docs__version b").TextContent.ShouldBe($"v{DocumentationLibrary.Version}");
    _context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
  }

  [Fact]
  public void Render_Page_ShouldShowItsSourceContentsAndNeighbours()
  {
    var cut = Render<Docs>(WithContext("organization-onboarding"));

    cut.WaitForAssertion(() => cut.Find("h1.cc-docs__title").TextContent.ShouldBe("Onboarding an organization"));
    cut.Find(".cc-docs__meta .cc-mono").TextContent.ShouldBe("docs/operations/organization-onboarding.md");
    cut.FindAll(".cc-docs__toc a").Select(static a => a.GetAttribute("href")).ShouldContain("docs/organization-onboarding#2-admit-it");
    cut.Find(".cc-md").GetAttribute("lang").ShouldBe("en");
    cut.Find(".cc-md h2[id='2-admit-it'] a.cc-md__anchor").GetAttribute("href").ShouldBe("docs/organization-onboarding#2-admit-it");

    var pager = cut.FindAll(".cc-docs__pager a").Select(static a => a.GetAttribute("href")).ToList();
    pager.ShouldBe(["docs/operations", "docs/github-checklist"]);
  }

  [Fact]
  public void Render_FirstPage_ShouldOfferOnlyTheNextPage()
  {
    var cut = Render<Docs>(WithContext("operations"));

    cut.WaitForAssertion(() => cut.Find(".cc-docs__pager a").GetAttribute("href").ShouldBe("docs/organization-onboarding"));
    cut.FindAll(".cc-docs__pager a").ShouldHaveSingleItem().ClassList.ShouldContain("cc-docs__next");
  }

  // A document outside the whitelist: the navigation stays, the article says so, and the server answers 404.
  [Theory]
  [InlineData("plan")]
  [InlineData("ARCHITECTURE")]
  [InlineData("adr")]
  public void Render_UnknownPage_ShouldKeepTheNavigationAndAnswer404(string slug)
  {
    var cut = Render<Docs>(WithContext(slug));

    cut.WaitForAssertion(() => cut.Find("[data-state=unknown]").TextContent.ShouldContain(L("Docs_UnknownTitle")));
    cut.FindAll(".cc-docs__link").Count.ShouldBe(DocumentationCatalog.Entries.Count);
    cut.FindAll(".cc-docs__link[aria-current]").ShouldBeEmpty();
    cut.Find("[data-state=unknown] a.cc-button").GetAttribute("href").ShouldBe("docs/operations");
    cut.FindAll(".cc-md").ShouldBeEmpty();
    _context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
  }

  // On a narrow screen, the navigation folds behind a button that names the current section and page.
  [Fact]
  public void ToggleNavigation_ShouldUnfoldIt()
  {
    var cut = Render<Docs>(WithContext("github-actions"));
    cut.WaitForAssertion(() => cut.Find("h1.cc-docs__title"));

    var toggle = cut.Find(".cc-docs__nav-toggle");
    toggle.GetAttribute("aria-expanded").ShouldBe("false");
    toggle.TextContent.ShouldContain(L("Docs_Section_ConfigureRepository"));

    toggle.Click();

    cut.Find(".cc-docs").ClassList.ShouldContain("cc-docs--nav-open");
    cut.Find(".cc-docs__nav-toggle").GetAttribute("aria-expanded").ShouldBe("true");
  }

  // No translation is published yet: the French console serves the English document, marked as English.
  [Fact]
  public void Render_InFrench_ShouldServeTheEnglishDocumentAndFrenchChrome()
  {
    var previous = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

    try
    {
      var cut = Render<Docs>(WithContext("operations"));

      cut.WaitForAssertion(() => cut.Find(".cc-md").GetAttribute("lang").ShouldBe("en"));
      cut.Find(".cc-docs__toc h2").TextContent.ShouldBe("Sur cette page");
    }
    finally
    {
      CultureInfo.CurrentUICulture = previous;
    }
  }

  private Action<ComponentParameterCollectionBuilder<Docs>> WithContext(string? slug = null) =>
    p => p.Add(static d => d.Slug, slug).AddCascadingValue(_context);

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
