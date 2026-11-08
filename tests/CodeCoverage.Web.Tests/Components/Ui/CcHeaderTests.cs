// ------------------------------------------------------------------------------------------------
// <copyright file="CcHeaderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcHeaderTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_ShouldRenderTheTitleAsTheHeadingOfThePage()
  {
    var cut = Render<CcHeader>(p => p.Add(h => h.Title, "Repositories")
                                     .Add(h => h.Icon, CcIconName.Repo)
                                     .Add(h => h.Subtitle, "Accessible repositories"));

    cut.Find("h1.cc-header__title").TextContent.Trim().ShouldBe("Repositories");
    cut.Find("h1 svg").GetAttribute("aria-hidden").ShouldBe("true");
    cut.Find(".cc-header__subtitle").TextContent.ShouldBe("Accessible repositories");
    cut.FindAll("nav").ShouldBeEmpty();
    cut.FindAll(".cc-header__actions").ShouldBeEmpty();
  }

  [Fact]
  public void Render_WithBreadcrumb_ShouldLinkThePreviousStepsAndMarkTheLastCurrent()
  {
    CcBreadcrumbItem[] steps = [new("my-org", "repositories"), new("sample-repo", "org/sample-repo"), new("Commits", "ignored")];

    var cut = Render<CcHeader>(p => p.Add(h => h.Title, "Commits").Add(h => h.Breadcrumb, steps));

    var nav = cut.Find("nav.cc-breadcrumb");
    nav.GetAttribute("aria-label").ShouldBe(Text("Breadcrumb"));
    cut.FindAll("nav a").Select(a => a.GetAttribute("href")).ShouldBe(["repositories", "org/sample-repo"]);
    cut.Find("[aria-current=page]").TextContent.ShouldBe("Commits");
    cut.FindAll(".cc-breadcrumb__sep").Count.ShouldBe(2);
    cut.FindAll(".cc-breadcrumb__sep").ShouldAllBe(s => s.GetAttribute("aria-hidden") == "true");
  }

  [Fact]
  public void Render_WithActionsAndTabs_ShouldRenderThem()
  {
    var cut = Render<CcHeader>(p => p.Add(h => h.Title, "Overview")
                                     .Add(h => h.Actions, (RenderFragment)(b => b.AddMarkupContent(0, "<button>Re-notify</button>")))
                                     .Add(h => h.Tabs, (RenderFragment)(b => b.AddMarkupContent(0, "<nav class=\"tabs\"></nav>"))));

    cut.Find(".cc-header__actions button").TextContent.ShouldBe("Re-notify");
    cut.Find(".cc-header > nav.tabs").ShouldNotBeNull();
  }

  #endregion
}
