// ------------------------------------------------------------------------------------------------
// <copyright file="CcIconTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcIconTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_WithoutTitle_ShouldBeHiddenFromAssistiveTechnologies()
  {
    var cut = Render<CcIcon>(p => p.Add(i => i.Name, CcIconName.Repo));

    var svg = cut.Find("svg");
    svg.GetAttribute("aria-hidden").ShouldBe("true");
    svg.HasAttribute("role").ShouldBeFalse();
    svg.GetAttribute("stroke-width").ShouldBe("2");
    svg.GetAttribute("viewBox").ShouldBe("0 0 24 24");
    svg.ClassName.ShouldBe("cc-icon");
  }

  [Fact]
  public void Render_WithTitle_ShouldBeANamedImage()
  {
    var cut = Render<CcIcon>(p => p.Add(i => i.Name, CcIconName.Alert).Add(i => i.Title, "Warning").Add(i => i.Class, "big"));

    var svg = cut.Find("svg");
    svg.GetAttribute("role").ShouldBe("img");
    svg.GetAttribute("aria-label").ShouldBe("Warning");
    svg.HasAttribute("aria-hidden").ShouldBeFalse();
    svg.ClassName.ShouldBe("cc-icon big");
  }

  [Fact]
  public void Render_EveryIcon_ShouldDrawItsShapes()
  {
    foreach (var name in Enum.GetValues<CcIconName>())
    {
      var cut = Render<CcIcon>(p => p.Add(i => i.Name, name));

      cut.Find("svg").ChildElementCount.ShouldBeGreaterThan(0, $"the icon {name} has no shape");
    }
  }

  #endregion
}
