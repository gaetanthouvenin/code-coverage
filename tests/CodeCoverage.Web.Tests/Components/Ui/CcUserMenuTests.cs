// ------------------------------------------------------------------------------------------------
// <copyright file="CcUserMenuTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components.Web;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcUserMenuTests : UiTestContext
{
  #region Methods

  [Theory]
  [InlineData("gthouvenin", "GT")]
  [InlineData("Guillaume Thouvenin", "GT")]
  [InlineData("dependabot[bot]", "DE")]
  [InlineData("m.lefevre", "ML")]
  [InlineData("x", "X")]
  [InlineData("", "?")]
  [InlineData(null, "?")]
  public void InitialsOf_WithUserName_ShouldTakeTwoLetters(string? userName, string expected)
  {
    CcUserMenu.InitialsOf(userName).ShouldBe(expected);
  }

  [Fact]
  public void Render_WithHref_ShouldRenderALinkToTheAccount()
  {
    var cut = Render<CcUserMenu>(p => p.Add(u => u.UserName, "gthouvenin")
                                       .Add(u => u.RoleLabel, "Administrator")
                                       .Add(u => u.Href, "account/tokens"));

    var link = cut.Find("a.cc-user-menu__trigger");
    link.GetAttribute("href").ShouldBe("account/tokens");
    cut.Find(".cc-user-menu__avatar").TextContent.ShouldBe("GT");
    cut.Find(".cc-user-menu__avatar").GetAttribute("aria-hidden").ShouldBe("true");
    cut.Find(".cc-user-menu__role").TextContent.ShouldBe("Administrator");
  }

  [Fact]
  public void Click_WithMenuContent_ShouldDiscloseThePanel()
  {
    var cut = Render<CcUserMenu>(p => p.Add(u => u.UserName, "gthouvenin")
                                       .Add(u => u.Initials, "G")
                                       .AddChildContent("<a href=\"signout\">Sign out</a>"));
    var button = cut.Find("button.cc-user-menu__trigger");
    button.GetAttribute("aria-expanded").ShouldBe("false");
    button.GetAttribute("aria-label").ShouldBe($"{Text("AccountMenu")}, gthouvenin");
    cut.FindAll(".cc-user-menu__panel").ShouldBeEmpty();

    button.Click();

    var panel = cut.Find(".cc-user-menu__panel");
    cut.Find("button").GetAttribute("aria-expanded").ShouldBe("true");
    cut.Find("button").GetAttribute("aria-controls").ShouldBe(panel.Id);
    panel.TextContent.ShouldBe("Sign out");
    cut.Find(".cc-user-menu__avatar").TextContent.ShouldBe("G");
  }

  [Fact]
  public void Escape_ShouldCloseThePanel()
  {
    var cut = Render<CcUserMenu>(p => p.Add(u => u.UserName, "gthouvenin").AddChildContent("<a href=\"x\">X</a>"));
    cut.Find("button").Click();

    cut.Find("div.cc-user-menu").KeyDown(new KeyboardEventArgs { Key = "Escape" });

    cut.FindAll(".cc-user-menu__panel").ShouldBeEmpty();
    cut.Find("button").GetAttribute("aria-expanded").ShouldBe("false");
  }

  [Fact]
  public void Navigation_ShouldCloseThePanel()
  {
    var cut = Render<CcUserMenu>(p => p.Add(u => u.UserName, "gthouvenin").AddChildContent("<a href=\"x\">X</a>"));
    cut.Find("button").Click();

    Navigation.NavigateTo("account/tokens");

    cut.WaitForAssertion(() => cut.FindAll(".cc-user-menu__panel").ShouldBeEmpty());
  }

  [Fact]
  public void Render_WithoutHrefOrContent_ShouldBeAStaticIdentity()
  {
    var cut = Render<CcUserMenu>(p => p.Add(u => u.UserName, "gthouvenin"));

    cut.FindAll("a, button").ShouldBeEmpty();
    cut.Find(".cc-user-menu__name").TextContent.ShouldBe("gthouvenin");
  }

  #endregion
}
