// ------------------------------------------------------------------------------------------------
// <copyright file="CcTopNavTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcTopNavTests : UiTestContext
{
  #region Fields

  private static readonly CcNavItem[] Items =
  [
    new("repositories", "Repositories"),
    new("account/tokens", "My account"),
    new("admin", "Administration"),
  ];

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldNameTheBrandAndTheNavigationsAndMarkTheCurrentEntry()
  {
    Navigation.NavigateTo("admin/settings");

    var cut = Render<CcTopNav>(p => p.Add(n => n.Items, Items));

    cut.Find(".cc-topnav__brand").GetAttribute("aria-label").ShouldBe(string.Format(Text("Home"), Text("ProductName")));
    cut.Find(".cc-topnav__brand-mark").GetAttribute("aria-hidden").ShouldBe("true");
    cut.Find("nav.cc-topnav__tabs").GetAttribute("aria-label").ShouldBe(Text("MainNavigation"));
    cut.Find("nav.cc-topnav__mobile").GetAttribute("aria-label").ShouldBe(Text("MobileNavigation"));
    var current = cut.FindAll("nav.cc-topnav__tabs a[aria-current=page]");
    current.Count.ShouldBe(1);
    current[0].TextContent.ShouldBe("Administration");
    current[0].ClassList.ShouldContain("cc-topnav__tab--active");
  }

  [Fact]
  public void MenuButton_ShouldToggleTheMobileNavigation()
  {
    var cut = Render<CcTopNav>(p => p.Add(n => n.Items, Items));
    var mobile = cut.Find("nav.cc-topnav__mobile");
    var button = cut.Find(".cc-topnav__menu-button");
    button.GetAttribute("aria-controls").ShouldBe(mobile.Id);
    button.GetAttribute("aria-expanded").ShouldBe("false");
    button.GetAttribute("aria-label").ShouldBe(Text("OpenMenu"));

    button.Click();

    cut.Find(".cc-topnav__menu-button").GetAttribute("aria-expanded").ShouldBe("true");
    cut.Find(".cc-topnav__menu-button").GetAttribute("aria-label").ShouldBe(Text("CloseMenu"));
    cut.Find("nav.cc-topnav__mobile").ClassList.ShouldContain("cc-topnav__mobile--open");
  }

  [Fact]
  public void Navigation_ShouldCloseTheMobileNavigation()
  {
    var cut = Render<CcTopNav>(p => p.Add(n => n.Items, Items));
    cut.Find(".cc-topnav__menu-button").Click();

    Navigation.NavigateTo("account/tokens");

    cut.WaitForAssertion(() => cut.Find(".cc-topnav__menu-button").GetAttribute("aria-expanded").ShouldBe("false"));
  }

  [Fact]
  public void ThemeToggle_ShouldSwitchTheThemeAndNameTheNextOne()
  {
    JSInterop.Setup<bool>("ccUi.isDark").SetResult(false);
    JSInterop.Setup<bool>("ccUi.toggleTheme").SetResult(true);
    var cut = Render<CcTopNav>(p => p.Add(n => n.Items, Items));
    cut.Find(".cc-topnav__theme").GetAttribute("aria-label").ShouldBe(Text("ThemeToDark"));

    cut.Find(".cc-topnav__theme").Click();

    cut.Find(".cc-topnav__theme").GetAttribute("aria-label").ShouldBe(Text("ThemeToLight"));
    JSInterop.VerifyInvoke("ccUi.toggleTheme");
  }

  [Fact]
  public void Render_InDarkBrowser_ShouldOfferTheLightTheme()
  {
    JSInterop.Setup<bool>("ccUi.isDark").SetResult(true);

    var cut = Render<CcTopNav>(p => p.Add(n => n.Items, Items));

    cut.WaitForAssertion(() => cut.Find(".cc-topnav__theme").GetAttribute("aria-label").ShouldBe(Text("ThemeToLight")));
  }

  [Fact]
  public void Render_WithoutThemeToggleOrActions_ShouldOmitTheIconGroup()
  {
    var cut = Render<CcTopNav>(p => p.Add(n => n.Items, Items).Add(n => n.ShowThemeToggle, false));

    cut.FindAll(".cc-topnav__icons").ShouldBeEmpty();
  }

  [Fact]
  public void Render_WithActionsAndUserMenu_ShouldPlaceThemAtTheEnd()
  {
    var cut = Render<CcTopNav>(p => p.Add(n => n.ProductName, "Cov")
                                     .Add(n => n.Actions, (RenderFragment)(b => b.AddMarkupContent(0, "<button class=\"bell\">!</button>")))
                                     .Add(n => n.UserMenu, (RenderFragment)(b => b.AddMarkupContent(0, "<span class=\"me\">me</span>"))));

    cut.Find(".cc-topnav__icons .bell").ShouldNotBeNull();
    cut.Find(".cc-topnav__end .me").ShouldNotBeNull();
    cut.Find(".cc-topnav__brand").TextContent.Trim().ShouldBe("Cov");
  }

  // What follows the brand (the switcher of organization) is also at the top of the mobile menu.
  [Fact]
  public void Render_Start_ShouldFollowTheBrandAndOpenTheMobileMenu()
  {
    var cut = Render<CcTopNav>(p => p.Add(n => n.Items, Items).Add(n => n.Start, (RenderFragment)(b => b.AddMarkupContent(0, "<span class=\"start\">org</span>"))));

    cut.Find(".cc-topnav__start .cc-topnav__brand + .start").ShouldNotBeNull();
    cut.Find(".cc-topnav__mobile .start").ShouldNotBeNull();
  }

  #endregion
}
