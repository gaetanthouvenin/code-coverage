// ------------------------------------------------------------------------------------------------
// <copyright file="CcSparklineTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcSparklineTests : UiTestContext
{
  #region Methods

  // The coordinates go through the invariant culture: a French comma would break the path.
  [Fact]
  public void Render_FrenchCulture_ShouldWriteTheCoordinatesWithADot()
  {
    var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

    try
    {
      var cut = Render<CcSparkline>(p => p.Add(c => c.Values, [80.37d, 81.11d, 82.93d]).Add(c => c.AriaLabel, "De 80 à 82"));

      var names = new[] { "cx", "cy", "d" };
      var values = cut.FindAll("svg *").SelectMany(e => names.Select(e.GetAttribute)).OfType<string>().ToList();
      values.ShouldNotBeEmpty();
      values.ShouldAllBe(static v => !v.Contains(','));
    }
    finally
    {
      (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
    }
  }

  // A missing value is skipped: the line joins the known ones, and the accessible name is the text alternative.
  [Fact]
  public void Render_ShouldDrawTheKnownValuesWithTheirAccessibleName()
  {
    var cut = Render<CcSparkline>(p => p.Add(c => c.Values, [80d, null, 81d, 82d]).Add(c => c.AriaLabel, "From 80 to 82"));

    var svg = cut.Find("svg.cc-spark");
    svg.GetAttribute("role").ShouldBe("img");
    svg.GetAttribute("aria-label").ShouldBe("From 80 to 82");
    cut.Find(".cc-spark__line").GetAttribute("d")!.Count(static c => c == 'L').ShouldBe(2);
  }

  [Fact]
  public void Render_NoValue_ShouldShowADash()
  {
    var cut = Render<CcSparkline>(p => p.Add(c => c.Values, [null]).Add(c => c.AriaLabel, "None"));

    cut.FindAll("svg").ShouldBeEmpty();
    cut.Find(".cc-dim").TextContent.ShouldBe("—");
  }

  #endregion
}
