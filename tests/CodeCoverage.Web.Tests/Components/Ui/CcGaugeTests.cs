// ------------------------------------------------------------------------------------------------
// <copyright file="CcGaugeTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcGaugeTests : UiTestContext
{
  #region Methods

  // Three quarters lit, the rest empty; the accessible name says the caption and the value.
  [Fact]
  public void Render_ShouldLightTheShareOfTheValue()
  {
    var cut = Render<CcGauge>(p => p.Add(c => c.Value, 75).Add(c => c.Caption, "Remaining").Add(c => c.ValueText, "75%"));

    cut.Find("svg").GetAttribute("aria-label").ShouldBe("Remaining: 75%");
    var segments = cut.FindAll("rect");
    segments.Count.ShouldBe(16);
    segments.Count(static r => r.GetAttribute("style")!.Contains("--cc-gauge-off", StringComparison.Ordinal)).ShouldBe(4);
  }

  [Fact]
  public void Render_OutOfRange_ShouldBeBroughtBack()
  {
    var cut = Render<CcGauge>(p => p.Add(c => c.Value, 140).Add(c => c.Caption, "Remaining").Add(c => c.ValueText, "140%"));

    cut.FindAll("rect").ShouldAllBe(static r => !r.GetAttribute("style")!.Contains("--cc-gauge-off", StringComparison.Ordinal));
  }

  #endregion
}
