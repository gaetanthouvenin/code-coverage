// ------------------------------------------------------------------------------------------------
// <copyright file="CcEmptyStateTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcEmptyStateTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_ShouldRenderTitleDescriptionAndActions()
  {
    var cut = Render<CcEmptyState>(p => p.Add(e => e.Title, "Not found")
                                         .Add(e => e.Icon, CcIconName.Repo)
                                         .AddChildContent("No such repository.")
                                         .Add(e => e.Actions, (RenderFragment)(b => b.AddMarkupContent(0, "<a href=\"\">Back</a>"))));

    cut.Find("h2.cc-empty-state__title").TextContent.ShouldBe("Not found");
    cut.Find(".cc-empty-state__description").TextContent.ShouldBe("No such repository.");
    cut.Find(".cc-empty-state__actions a").TextContent.ShouldBe("Back");
    cut.Find(".cc-empty-state__icon").GetAttribute("aria-hidden").ShouldBe("true");
  }

  [Fact]
  public void Render_WithTitleOnly_ShouldOmitDescriptionAndActions()
  {
    var cut = Render<CcEmptyState>(p => p.Add(e => e.Title, "Nothing yet"));

    cut.FindAll(".cc-empty-state__description, .cc-empty-state__actions").ShouldBeEmpty();
  }

  #endregion
}
