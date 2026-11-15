// ------------------------------------------------------------------------------------------------
// <copyright file="CcLanguagePickerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

// The language picker of the mockup M4.02q: each language named in itself, the current one marked, each link back to
// the current page through /culture.
public sealed class CcLanguagePickerTests : UiTestContext
{
  #region Methods

  [Theory]
  [InlineData(CcLanguagePickerVariant.Menu, "cc-lang")]
  [InlineData(CcLanguagePickerVariant.Row, "cc-lang cc-lang--row")]
  [InlineData(CcLanguagePickerVariant.Inline, "cc-lang cc-lang--inline")]
  public void Render_InEachVariant_ShouldLinkEveryLanguageBackToTheCurrentPage(CcLanguagePickerVariant variant, string cssClass)
  {
    Navigation.NavigateTo("github/octo-org/app/commits?branch=main");

    var cut = Render<CcLanguagePicker>(p => p.Add(c => c.Variant, variant));

    cut.Find("[role=group]").ClassName.ShouldBe(cssClass);
    var links = cut.FindAll(".cc-lang__options a");
    links.Select(static a => a.TextContent).ShouldBe(["English", "Français", "Italiano", "Español"]);
    links.Select(static a => a.GetAttribute("lang")).ShouldBe(["en", "fr", "it", "es"]);
    links.Select(static a => a.GetAttribute("hreflang")).ShouldBe(["en", "fr", "it", "es"]);
    links.ShouldAllBe(static a => a.GetAttribute("data-enhance-nav") == "false");
    links[2].GetAttribute("href").ShouldBe("culture?lang=it&returnUrl=%2Fgithub%2Focto-org%2Fapp%2Fcommits%3Fbranch%3Dmain");
  }

  [Theory]
  [InlineData("fr-FR", "Français")]
  [InlineData("es-MX", "Español")]
  [InlineData("de-DE", "English")]
  public void Render_InACulture_ShouldMarkItsLanguageAsCurrent(string culture, string current)
  {
    var previous = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    try
    {
      var cut = Render<CcLanguagePicker>();

      cut.FindAll("a[aria-current=true]").ShouldHaveSingleItem().TextContent.ShouldBe(current);
    }
    finally
    {
      CultureInfo.CurrentUICulture = previous;
    }
  }

  [Fact]
  public void Click_LanguageMenu_ShouldOpenThePickerAndShowTheCodeOfTheLanguage()
  {
    var previous = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");

    try
    {
      var cut = Render<CcLanguageMenu>();
      var button = cut.Find("button.cc-lang-menu__trigger");

      cut.Find(".cc-lang-menu__code").TextContent.ShouldBe("IT");
      button.GetAttribute("aria-label").ShouldBe("Lingua (Italiano)");
      button.GetAttribute("aria-expanded").ShouldBe("false");
      cut.FindAll(".cc-lang-menu__panel").ShouldBeEmpty();

      button.Click();

      cut.Find("button.cc-lang-menu__trigger").GetAttribute("aria-expanded").ShouldBe("true");
      cut.Find(".cc-lang-menu__panel .cc-lang").ShouldNotBeNull();
    }
    finally
    {
      CultureInfo.CurrentUICulture = previous;
    }
  }

  #endregion
}
