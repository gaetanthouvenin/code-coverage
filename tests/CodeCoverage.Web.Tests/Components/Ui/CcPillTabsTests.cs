// ------------------------------------------------------------------------------------------------
// <copyright file="CcPillTabsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components.Routing;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcPillTabsTests : UiTestContext
{
  #region Fields

  private static readonly CcNavItem[] Items =
  [
    new("org/repo", "Overview", CcIconName.Chart, NavLinkMatch.All),
    new("org/repo/commits", "Commits", CcIconName.Commit),
    new("org/repo/settings", "Settings", CcIconName.Gear),
  ];

  #endregion

  #region Methods

  [Fact]
  public void Render_OnMatchingAddress_ShouldMarkTheCurrentTab()
  {
    Navigation.NavigateTo("org/repo/commits/e4f5a6b");

    var cut = Render<CcPillTabs>(p => p.Add(t => t.Items, Items).Add(t => t.AriaLabel, "Repository sections"));

    cut.Find("nav").GetAttribute("aria-label").ShouldBe("Repository sections");
    var links = cut.FindAll("a");
    links.Select(l => l.GetAttribute("aria-current")).ShouldBe([null, "page", null]);
    links[1].ClassName.ShouldBe("cc-pill-tabs__tab cc-pill-tabs__tab--active");
    links[0].QuerySelector("svg")!.GetAttribute("aria-hidden").ShouldBe("true");
  }

  [Fact]
  public void Navigation_ShouldMoveTheCurrentTab()
  {
    Navigation.NavigateTo("org/repo");
    var cut = Render<CcPillTabs>(p => p.Add(t => t.Items, Items).Add(t => t.AriaLabel, "Repository sections"));

    Navigation.NavigateTo("org/repo/settings");

    cut.WaitForAssertion(() => cut.FindAll("a")[2].GetAttribute("aria-current").ShouldBe("page"));
    cut.FindAll("a")[0].HasAttribute("aria-current").ShouldBeFalse();
  }

  #endregion
}
