// ------------------------------------------------------------------------------------------------
// <copyright file="CcBadgeTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcBadgeTests : UiTestContext
{
  #region Methods

  [Theory]
  [InlineData(CcTone.Neutral, "cc-badge--neutral")]
  [InlineData(CcTone.Info, "cc-badge--info")]
  [InlineData(CcTone.Success, "cc-badge--success")]
  [InlineData(CcTone.Warning, "cc-badge--warning")]
  [InlineData(CcTone.Danger, "cc-badge--danger")]
  public void Render_WithTone_ShouldApplyItsModifier(CcTone tone, string expected)
  {
    var cut = Render<CcBadge>(p => p.Add(b => b.Tone, tone).AddChildContent("Processed"));

    var badge = cut.Find("span.cc-badge");
    badge.ClassList.ShouldContain(expected);
    badge.ClassList.ShouldNotContain("cc-badge--chip");
    badge.TextContent.Trim().ShouldBe("Processed");
  }

  [Fact]
  public void Render_AsChipWithIcon_ShouldApplyChipModifierAndHideTheIcon()
  {
    var cut = Render<CcBadge>(p => p.Add(b => b.Chip, true).Add(b => b.Icon, CcIconName.Check).Add(b => b.Class, "extra"));

    var badge = cut.Find("span");
    badge.ClassName.ShouldBe("cc-badge cc-badge--neutral cc-badge--chip extra");
    cut.Find("svg").GetAttribute("aria-hidden").ShouldBe("true");
  }

  #endregion
}
