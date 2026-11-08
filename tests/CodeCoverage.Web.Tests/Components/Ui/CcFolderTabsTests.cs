// ------------------------------------------------------------------------------------------------
// <copyright file="CcFolderTabsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components.Web;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcFolderTabsTests : UiTestContext
{
  #region Fields

  private static readonly CcTabItem[] Tabs = [new("30", "30 days"), new("90", "90 days"), new("365", "1 year")];

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldFollowTheTabsPatternWithARovingTabStop()
  {
    var cut = Render<CcFolderTabs>(p => p.Add(t => t.Tabs, Tabs)
                                         .Add(t => t.Value, "90")
                                         .Add(t => t.AriaLabel, "Range")
                                         .Add(t => t.PanelId, "range-panel"));

    var list = cut.Find("[role=tablist]");
    list.GetAttribute("aria-label").ShouldBe("Range");
    var tabs = cut.FindAll("[role=tab]");
    tabs.Count.ShouldBe(3);
    tabs.Select(t => t.GetAttribute("aria-selected")).ShouldBe(["false", "true", "false"]);
    tabs.Select(t => t.GetAttribute("tabindex")).ShouldBe(["-1", "0", "-1"]);
    tabs.ShouldAllBe(t => t.GetAttribute("aria-controls") == "range-panel");
    tabs[1].ClassList.ShouldContain("cc-folder-tabs__tab--active");
  }

  [Fact]
  public void Render_WithoutValue_ShouldSelectTheFirstTab()
  {
    var cut = Render<CcFolderTabs>(p => p.Add(t => t.Tabs, Tabs));

    cut.FindAll("[role=tab]")[0].GetAttribute("aria-selected").ShouldBe("true");
  }

  [Fact]
  public void Click_ShouldRaiseValueChanged()
  {
    string? value = null;
    var cut = Render<CcFolderTabs>(p => p.Add(t => t.Tabs, Tabs).Add(t => t.ValueChanged, v => value = v));

    cut.FindAll("[role=tab]")[2].Click();

    value.ShouldBe("365");
  }

  [Theory]
  [InlineData(0, "ArrowRight", "90")]
  [InlineData(2, "ArrowRight", "30")]
  [InlineData(0, "ArrowLeft", "365")]
  [InlineData(1, "Home", "30")]
  [InlineData(0, "End", "365")]
  public void KeyDown_WithNavigationKey_ShouldSelectAndFocusAnotherTab(int from, string key, string expected)
  {
    var cut = Render<CcFolderTabs>(p => p.Add(t => t.Tabs, Tabs).Add(t => t.Value, Tabs[from].Id));

    cut.FindAll("[role=tab]")[from].KeyDown(new KeyboardEventArgs { Key = key });

    var selected = cut.Find("[aria-selected=true]");
    selected.TextContent.Trim().ShouldBe(Tabs.Single(t => t.Id == expected).Label);
    selected.GetAttribute("tabindex").ShouldBe("0");
    JSInterop.Invocations.ShouldContain(i => i.Identifier.EndsWith("focus", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public void KeyDown_WithOtherKey_ShouldKeepTheSelection()
  {
    string? value = null;
    var cut = Render<CcFolderTabs>(p => p.Add(t => t.Tabs, Tabs).Add(t => t.ValueChanged, v => value = v));

    cut.FindAll("[role=tab]")[0].KeyDown(new KeyboardEventArgs { Key = "a" });

    value.ShouldBeNull();
  }

  #endregion
}
