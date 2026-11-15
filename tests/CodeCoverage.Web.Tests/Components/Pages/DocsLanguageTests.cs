// ------------------------------------------------------------------------------------------------
// <copyright file="DocsLanguageTests.cs" company="Gaëtan Thouvenin">
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

// The page of a document by language (M4.02v, mockup #docs--untranslated), on the sample of the test assembly.
public sealed class DocsLanguageTests : UiTestContext
{
  #region Fields

  private readonly DefaultHttpContext _context = new();

  #endregion

  #region Ctors

  public DocsLanguageTests()
  {
    Services.AddSingleton(static s => new DocumentationLibrary(s.GetRequiredService<IStringLocalizer<Common>>(), typeof(DocsLanguageTests).Assembly));
  }

  #endregion

  #region Methods

  [Theory]
  [InlineData("es-ES", "Esta página aún no está traducida al español: se muestra en inglés, el idioma de referencia de la documentación.")]
  [InlineData("fr-FR", "Cette page n’est pas encore traduite en français : elle s’affiche en anglais, la langue de référence de la documentation.")]
  public void Render_WithoutTranslation_ShouldAnnounceTheEnglishPage(string culture, string banner)
  {
    var cut = RenderIn(culture);

    cut.Find(".cc-docs__article .cc-banner").TextContent.Trim().ShouldBe(banner);
    cut.Find(".cc-md").GetAttribute("lang").ShouldBe("en");
  }

  [Theory]
  [InlineData("it-IT", "it")]
  [InlineData("en-US", "en")]
  public void Render_InTheLanguageOfTheDocument_ShouldShowNoBanner(string culture, string language)
  {
    var cut = RenderIn(culture);

    cut.FindAll(".cc-docs__article .cc-banner").ShouldBeEmpty();
    cut.Find(".cc-md").GetAttribute("lang").ShouldBe(language);
  }

  private IRenderedComponent<Docs> RenderIn(string culture)
  {
    var previous = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    try
    {
      var cut = Render<Docs>(p => p.Add(static d => d.Slug, "operations").AddCascadingValue(_context));
      cut.WaitForAssertion(() => cut.Find(".cc-md"));

      return cut;
    }
    finally
    {
      CultureInfo.CurrentUICulture = previous;
    }
  }

  #endregion
}
