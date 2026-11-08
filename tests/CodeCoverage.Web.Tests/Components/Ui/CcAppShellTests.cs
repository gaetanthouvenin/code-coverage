// ------------------------------------------------------------------------------------------------
// <copyright file="CcAppShellTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcAppShellTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_ShouldOfferASkipLinkToTheMainLandmark()
  {
    Navigation.NavigateTo("org/repo?branch=main#top");

    var cut = Render<CcAppShell>(p => p.Add(s => s.TopNav, (RenderFragment)(b => b.AddMarkupContent(0, "<header class=\"bar\"></header>")))
                                       .AddChildContent("<p>content</p>"));

    var skip = cut.Find("a.cc-skip-link");
    skip.TextContent.ShouldBe(Text("SkipToContent"));
    skip.GetAttribute("href").ShouldBe("org/repo?branch=main#cc-main");
    var main = cut.Find("main#cc-main");
    main.GetAttribute("tabindex").ShouldBe("-1");
    main.TextContent.Trim().ShouldBe("content");
    cut.Find(".cc-shell__top .bar").ShouldNotBeNull();
  }

  [Fact]
  public void Render_WithoutTopNav_ShouldOmitTheStickyBar()
  {
    var cut = Render<CcAppShell>(p => p.Add(s => s.MainId, "content").Add(s => s.Class, "extra"));

    cut.FindAll(".cc-shell__top").ShouldBeEmpty();
    cut.Find("div.cc-shell").ClassName.ShouldBe("cc-shell extra");
    cut.Find("a.cc-skip-link").GetAttribute("href").ShouldBe("#content");
  }

  #endregion
}
