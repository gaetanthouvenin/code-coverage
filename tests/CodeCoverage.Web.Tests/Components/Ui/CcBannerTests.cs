// ------------------------------------------------------------------------------------------------
// <copyright file="CcBannerTests.cs" company="Gaëtan Thouvenin">
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

public sealed class CcBannerTests : UiTestContext
{
  #region Methods

  [Theory]
  [InlineData(CcTone.Warning, "alert")]
  [InlineData(CcTone.Danger, "alert")]
  [InlineData(CcTone.Info, "status")]
  [InlineData(CcTone.Success, "status")]
  [InlineData(CcTone.Neutral, "status")]
  public void Render_WithTone_ShouldUseTheMatchingLiveRegionRole(CcTone tone, string role)
  {
    var cut = Render<CcBanner>(p => p.Add(b => b.Tone, tone));

    var banner = cut.Find("div.cc-banner");
    banner.GetAttribute("role").ShouldBe(role);
    banner.ClassList.ShouldContain($"cc-banner--{tone.ToString().ToLowerInvariant()}");
  }

  [Fact]
  public void Render_WithTitleBodyAndActions_ShouldRenderThem()
  {
    var cut = Render<CcBanner>(p => p.Add(b => b.Title, "Upload failed")
                                     .AddChildContent("Unknown format.")
                                     .Add(b => b.Actions, (RenderFragment)(b => b.AddContent(0, "Retry"))));

    cut.Find(".cc-banner__title").TextContent.ShouldBe("Upload failed");
    cut.Find(".cc-banner__body").TextContent.ShouldContain("Unknown format.");
    cut.Find(".cc-banner__actions").TextContent.ShouldBe("Retry");
  }

  [Fact]
  public void Render_WithoutIcon_ShouldDrawNoIcon()
  {
    var cut = Render<CcBanner>(p => p.Add(b => b.ShowIcon, false));

    cut.FindAll("svg").ShouldBeEmpty();
  }

  [Theory]
  [InlineData(CcTone.Success, CcIconName.Check)]
  [InlineData(CcTone.Warning, CcIconName.Alert)]
  [InlineData(CcTone.Danger, CcIconName.Alert)]
  [InlineData(CcTone.Info, CcIconName.Info)]
  [InlineData(CcTone.Neutral, CcIconName.Info)]
  public void DefaultIcon_WithTone_ShouldMatchTheTone(CcTone tone, CcIconName expected)
  {
    CcBanner.DefaultIcon(tone).ShouldBe(expected);
  }

  #endregion
}
