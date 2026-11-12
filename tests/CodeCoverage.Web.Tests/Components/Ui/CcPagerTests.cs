// ------------------------------------------------------------------------------------------------
// <copyright file="CcPagerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcPagerTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_MiddlePage_ShouldShowTheRangeAndBothButtons()
  {
    var asked = 0;

    var cut = Render<CcPager>(p => p.Add(c => c.Page, 2).Add(c => c.PageSize, 25).Add(c => c.TotalCount, 60).Add(c => c.PageChanged, page => asked = page));

    cut.Find(".cc-panel__foot span").TextContent.ShouldBe(Text("PagerRange", "26", "50", "60"));
    var buttons = cut.FindAll("button");
    buttons.ShouldAllBe(static b => !b.HasAttribute("disabled"));
    buttons[1].Click();
    asked.ShouldBe(3);
    cut.FindAll("button")[0].Click();
    asked.ShouldBe(1);
  }

  [Fact]
  public void Render_LastPage_ShouldDisableNext()
  {
    var cut = Render<CcPager>(p => p.Add(c => c.Page, 3).Add(c => c.PageSize, 25).Add(c => c.TotalCount, 60));

    cut.Find(".cc-panel__foot span").TextContent.ShouldBe(Text("PagerRange", "51", "60", "60"));
    cut.FindAll("button")[1].HasAttribute("disabled").ShouldBeTrue();
  }

  [Fact]
  public void Render_FirstPage_ShouldDisablePrevious()
  {
    var cut = Render<CcPager>(p => p.Add(c => c.Page, 1).Add(c => c.PageSize, 25).Add(c => c.TotalCount, 0));

    cut.Find(".cc-panel__foot span").TextContent.ShouldBe(Text("PagerEmpty"));
    cut.FindAll("button").ShouldAllBe(static b => b.HasAttribute("disabled"));
  }

  private string Text(string key, params string[] arguments) =>
    string.Format(CultureInfo.CurrentCulture, base.Text(key), arguments);

  #endregion
}
