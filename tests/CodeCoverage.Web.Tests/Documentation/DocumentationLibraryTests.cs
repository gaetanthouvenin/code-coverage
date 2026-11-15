// ------------------------------------------------------------------------------------------------
// <copyright file="DocumentationLibraryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Documentation;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Tests.Documentation;

// The documentation by language (M4.02v), on the sample of the test assembly: a translation is served with the anchors
// of the English document, a page without translation is served in English and marked as a fallback.
public sealed class DocumentationLibraryTests : IDisposable
{
  #region Fields

  private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

  private readonly ServiceProvider _services;

  private readonly DocumentationLibrary _library;

  #endregion

  #region Ctors

  public DocumentationLibraryTests()
  {
    _services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
    _library = new DocumentationLibrary(_services.GetRequiredService<IStringLocalizer<Common>>(), typeof(DocumentationLibraryTests).Assembly);
  }

  #endregion

  #region Properties

  private static DocumentEntry Sample => DocumentationCatalog.Find("operations")!;

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task GetAsync_InALanguageWithItsTranslation_ShouldServeItWithTheEnglishAnchors()
  {
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");

    var document = await _library.GetAsync(Sample, Ct);

    document.Language.ShouldBe("it");
    document.IsFallback.ShouldBeFalse();
    document.Title.ShouldBe("Guida di esempio");
    document.Headings.Select(static h => (h.Id, h.Text)).ShouldBe([("first-deployment", "Primo deployment"), ("prepare-the-host", "Preparare l’host"), ("restore", "Ripristino")]);
    document.Html.ShouldContain("id=\"first-deployment\"");
    document.Html.ShouldContain("href=\"docs/operations#restore\"");
    document.Html.ShouldNotContain("source-sha256");
    document.Html.ShouldContain("Nota");
  }

  [Fact]
  public async Task GetAsync_InALanguageWithoutTranslation_ShouldServeTheEnglishDocumentAsAFallback()
  {
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-MX");

    var document = await _library.GetAsync(Sample, Ct);

    document.Language.ShouldBe("en");
    document.RequestedLanguage.ShouldBe("es");
    document.IsFallback.ShouldBeTrue();
    document.Title.ShouldBe("Sample guide");
  }

  [Theory]
  [InlineData("en-US")]
  [InlineData("de-DE")]
  public async Task GetAsync_InEnglishOrALanguageOutsideTheConsole_ShouldNeverBeAFallback(string culture)
  {
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    var document = await _library.GetAsync(Sample, Ct);

    document.Language.ShouldBe("en");
    document.IsFallback.ShouldBeFalse();
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    CultureInfo.CurrentUICulture = _uiCulture;
    _services.Dispose();
  }

  #endregion
}
