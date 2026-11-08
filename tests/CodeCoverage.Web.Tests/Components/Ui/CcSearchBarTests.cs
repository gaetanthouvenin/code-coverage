// ------------------------------------------------------------------------------------------------
// <copyright file="CcSearchBarTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcSearchBarTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_WithoutPlaceholder_ShouldUseTheLocalizedSearchAsNameAndPlaceholder()
  {
    var cut = Render<CcSearchBar>();

    var input = cut.Find("input");
    cut.Find("div.cc-search").GetAttribute("role").ShouldBe("search");
    input.GetAttribute("type").ShouldBe("search");
    input.GetAttribute("placeholder").ShouldBe(Text("Search"));
    input.GetAttribute("aria-label").ShouldBe(Text("Search"));
  }

  [Fact]
  public void Render_WithAriaLabel_ShouldNameTheInputWithIt()
  {
    var cut = Render<CcSearchBar>(p => p.Add(s => s.Placeholder, "Search a file").Add(s => s.AriaLabel, "Files"));

    var input = cut.Find("input");
    input.GetAttribute("placeholder").ShouldBe("Search a file");
    input.GetAttribute("aria-label").ShouldBe("Files");
  }

  [Fact]
  public void Input_ShouldRaiseValueChanged()
  {
    string? value = null;
    var cut = Render<CcSearchBar>(p => p.Add(s => s.ValueChanged, v => value = v));

    cut.Find("input").Input("notify");

    value.ShouldBe("notify");
  }

  #endregion
}
