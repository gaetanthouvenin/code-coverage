// ------------------------------------------------------------------------------------------------
// <copyright file="CcGaugeTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcGaugeTests : UiTestContext
{
  #region Methods

  // Blazor writes a double in the current culture: a French "147,5" made the browser draw every segment at y=0.
  [Fact]
  public void Render_FrenchCulture_ShouldWriteTheCoordinatesWithADot()
  {
    var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

    try
    {
      var cut = Render<CcGauge>(p => p.Add(c => c.Value, 42.5).Add(c => c.Caption, "Restant"));

      var values = cut.FindAll("rect, text").SelectMany(static e => new[] { "x", "y", "transform" }.Select(e.GetAttribute)).OfType<string>().ToList();
      values.ShouldNotBeEmpty();
      values.ShouldAllBe(static v => !v.Contains(','));
      cut.Find("rect").GetAttribute("y").ShouldBe("147.5");
    }
    finally
    {
      (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
    }
  }

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
