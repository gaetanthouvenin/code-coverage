// ------------------------------------------------------------------------------------------------
// <copyright file="CcSwitchTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcSwitchTests : UiTestContext
{
  #region Methods

  [Theory]
  [InlineData(true, "true", "cc-switch cc-switch--on")]
  [InlineData(false, "false", "cc-switch")]
  public void Render_ShouldBeASwitchWithItsState(bool on, string checkedValue, string css)
  {
    var cut = Render<CcSwitch>(p => p.Add(s => s.Value, on).Add(s => s.Label, "Comments"));

    var button = cut.Find("button");
    button.GetAttribute("role").ShouldBe("switch");
    button.GetAttribute("aria-checked").ShouldBe(checkedValue);
    button.GetAttribute("aria-label").ShouldBe("Comments");
    button.ClassName.ShouldBe(css);
  }

  [Fact]
  public void Click_ShouldGiveTheOtherValue()
  {
    bool? changed = null;
    var cut = Render<CcSwitch>(p => p.Add(s => s.Value, false).Add(s => s.Label, "Comments").Add(s => s.ValueChanged, v => changed = v));

    cut.Find("button").Click();

    changed.ShouldBe(true);
  }

  #endregion
}
