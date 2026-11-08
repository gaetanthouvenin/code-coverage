// ------------------------------------------------------------------------------------------------
// <copyright file="CcButtonTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcButtonTests : UiTestContext
{
  #region Methods

  [Theory]
  [InlineData(CcButtonVariant.Secondary, "cc-button")]
  [InlineData(CcButtonVariant.Primary, "cc-button cc-button--primary")]
  [InlineData(CcButtonVariant.Danger, "cc-button cc-button--danger")]
  [InlineData(CcButtonVariant.SolidDanger, "cc-button cc-button--solid-danger")]
  [InlineData(CcButtonVariant.Pill, "cc-button cc-button--pill")]
  [InlineData(CcButtonVariant.PillAccent, "cc-button cc-button--pill-accent")]
  public void Render_WithVariant_ShouldApplyItsModifier(CcButtonVariant variant, string expected)
  {
    var cut = Render<CcButton>(p => p.Add(b => b.Variant, variant).AddChildContent("Save"));

    var button = cut.Find("button");
    button.ClassName.ShouldBe(expected);
    button.GetAttribute("type").ShouldBe("button");
    button.TextContent.Trim().ShouldBe("Save");
  }

  [Fact]
  public void Render_Small_ShouldApplySmallModifierExceptOnPills()
  {
    var small = Render<CcButton>(p => p.Add(b => b.Size, CcButtonSize.Small));
    var pill = Render<CcButton>(p => p.Add(b => b.Size, CcButtonSize.Small).Add(b => b.Variant, CcButtonVariant.Pill));

    small.Find("button").ClassList.ShouldContain("cc-button--small");
    pill.Find("button").ClassList.ShouldNotContain("cc-button--small");
  }

  [Fact]
  public void Render_PillWithIcon_ShouldDrawTheIconInARound()
  {
    var cut = Render<CcButton>(p => p.Add(b => b.Variant, CcButtonVariant.Pill).Add(b => b.Icon, CcIconName.Plus));

    cut.Find(".cc-button__round svg.cc-icon").GetAttribute("aria-hidden").ShouldBe("true");
  }

  [Fact]
  public void Render_WithHref_ShouldRenderALink()
  {
    var cut = Render<CcButton>(p => p.Add(b => b.Href, "admin/settings").AddChildContent("Settings"));

    cut.FindAll("button").ShouldBeEmpty();
    cut.Find("a.cc-button").GetAttribute("href").ShouldBe("admin/settings");
  }

  [Fact]
  public void Render_DisabledLink_ShouldDropHrefAndBeAriaDisabled()
  {
    var cut = Render<CcButton>(p => p.Add(b => b.Href, "admin/settings").Add(b => b.Disabled, true));

    var link = cut.Find("a");
    link.HasAttribute("href").ShouldBeFalse();
    link.GetAttribute("aria-disabled").ShouldBe("true");
  }

  [Fact]
  public void Render_Disabled_ShouldSetDisabledAttribute()
  {
    var cut = Render<CcButton>(p => p.Add(b => b.Disabled, true));

    cut.Find("button").HasAttribute("disabled").ShouldBeTrue();
  }

  [Fact]
  public void Render_IconOnlyWithAriaLabel_ShouldBeNamed()
  {
    var cut = Render<CcButton>(p => p.Add(b => b.Icon, CcIconName.Copy).Add(b => b.AriaLabel, "Copy"));

    cut.Find("button").GetAttribute("aria-label").ShouldBe("Copy");
  }

  [Fact]
  public void Click_ShouldInvokeOnClick()
  {
    var clicks = 0;
    var cut = Render<CcButton>(p => p.Add(b => b.OnClick, () => clicks++));

    cut.Find("button").Click();

    clicks.ShouldBe(1);
  }

  #endregion
}
