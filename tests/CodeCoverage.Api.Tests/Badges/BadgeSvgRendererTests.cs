// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeSvgRendererTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Api.Contracts.Badges;
using CodeCoverage.Api.Rendering;

namespace CodeCoverage.Api.Tests.Badges;

// The SVG is compared as text: a change of the template shows in the snapshot.
public sealed class BadgeSvgRendererTests
{
  #region Methods

  [Fact]
  public Task Render_Coverage_ShouldDrawTheFlatTemplate() => Verify(BadgeSvgRenderer.Render(new BadgeResponse("coverage", "85%", "#97ca00", "85")));

  [Fact]
  public Task Render_Unknown_ShouldDrawAGreyBadge() => Verify(BadgeSvgRenderer.Render(new BadgeResponse("coverage", "unknown", "#9f9f9f", "unknown")));

  // A flag is user data: it is escaped, and a character outside the width table still renders.
  [Fact]
  public void Render_FlagWithMarkup_ShouldEscapeIt()
  {
    var svg = BadgeSvgRenderer.Render(new BadgeResponse("<b>&é", "50.5%", "#dfb317", "50.5"));

    svg.ShouldContain("&lt;b&gt;&amp;é");
    svg.ShouldNotContain("<b>");
  }

  [Fact]
  public void Measure_CharacterOutsideTheTable_ShouldUseTheFallbackWidth() =>
    VerdanaWidthTable.Measure("é").ShouldBe((int)Math.Ceiling(VerdanaWidthTable.FallbackWidth));

  #endregion
}
