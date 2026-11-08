// ------------------------------------------------------------------------------------------------
// <copyright file="CcKpiCardTests.cs" company="Gaëtan Thouvenin">
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

public sealed class CcKpiCardTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_ShouldBeAGroupNamedByItsLabel()
  {
    var cut = Render<CcKpiCard>(p => p.Add(k => k.Label, "Project coverage")
                                      .Add(k => k.Value, "82.41 %")
                                      .Add(k => k.Trend, (RenderFragment)(b => b.AddContent(0, "+1.2 pt")))
                                      .Add(k => k.Footer, (RenderFragment)(b => b.AddContent(0, "Target 80 %"))));

    var card = cut.Find("div.cc-kpi");
    card.GetAttribute("role").ShouldBe("group");
    card.GetAttribute("aria-label").ShouldBe("Project coverage");
    card.ClassList.ShouldNotContain("cc-kpi--accent");
    cut.Find(".cc-kpi__value").TextContent.ShouldBe("82.41 %");
    cut.Find(".cc-kpi__row").TextContent.ShouldContain("+1.2 pt");
    cut.Find(".cc-kpi__footer").TextContent.ShouldBe("Target 80 %");
    cut.FindAll(".cc-kpi__icon").ShouldBeEmpty();
  }

  [Theory]
  [InlineData(CcKpiIconStyle.Soft, "cc-kpi__icon")]
  [InlineData(CcKpiIconStyle.Ink, "cc-kpi__icon cc-kpi__icon--ink")]
  [InlineData(CcKpiIconStyle.Gradient, "cc-kpi__icon cc-kpi__icon--gradient")]
  public void Render_WithIconStyle_ShouldStyleTheCornerRound(CcKpiIconStyle style, string expected)
  {
    var cut = Render<CcKpiCard>(p => p.Add(k => k.Label, "Uploads")
                                      .Add(k => k.Value, "3")
                                      .Add(k => k.Icon, CcIconName.Upload)
                                      .Add(k => k.IconStyle, style));

    var round = cut.Find("div.cc-kpi > span[aria-hidden=true]");
    round.ClassName.ShouldBe(expected);
  }

  [Fact]
  public void Render_Accent_ShouldUseTheGradientModifier()
  {
    var cut = Render<CcKpiCard>(p => p.Add(k => k.Label, "Coverage").Add(k => k.Value, "82 %").Add(k => k.Accent, true));

    cut.Find("div.cc-kpi").ClassList.ShouldContain("cc-kpi--accent");
  }

  #endregion
}
