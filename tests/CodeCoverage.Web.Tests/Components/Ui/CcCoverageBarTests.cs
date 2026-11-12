// ------------------------------------------------------------------------------------------------
// <copyright file="CcCoverageBarTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcCoverageBarTests : UiTestContext
{
  #region Methods

  // Each part takes its share of the lines; the bar is hidden, the text is read.
  [Fact]
  public void Render_ShouldGiveEachPartItsShare()
  {
    var cut = Render<CcCoverageBar>(p => p.Add(c => c.Hits, 46).Add(c => c.Partials, 3).Add(c => c.Misses, 11).Add(c => c.Text, "76.7%"));

    cut.Find(".cc-covbar__track").GetAttribute("aria-hidden").ShouldBe("true");
    cut.Find(".cc-covbar__h").GetAttribute("style").ShouldBe("width:76.67%");
    cut.Find(".cc-covbar__p").GetAttribute("style").ShouldBe("width:5%");
    cut.Find(".cc-covbar__m").GetAttribute("style").ShouldBe("width:18.33%");
    cut.Find(".cc-num").TextContent.ShouldBe("76.7%");
  }

  [Fact]
  public void Render_NoLine_ShouldShowTheTextOnly()
  {
    var cut = Render<CcCoverageBar>(p => p.Add(c => c.Text, "—"));

    cut.FindAll(".cc-covbar__track").ShouldBeEmpty();
    cut.Find(".cc-num").TextContent.ShouldBe("—");
  }

  #endregion
}
