// ------------------------------------------------------------------------------------------------
// <copyright file="CcLoadingSpinnerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcLoadingSpinnerTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_ShouldBeAStatusWithTheLocalizedLabel()
  {
    var cut = Render<CcLoadingSpinner>();

    var spinner = cut.Find(".cc-spinner");
    spinner.GetAttribute("role").ShouldBe("status");
    spinner.ClassName.ShouldBe("cc-spinner");
    cut.Find(".cc-spinner__ring").GetAttribute("aria-hidden").ShouldBe("true");
    cut.Find(".cc-spinner__label").TextContent.ShouldBe(Text("Loading"));
  }

  [Theory]
  [InlineData(CcSpinnerSize.Small, "cc-spinner cc-spinner--small")]
  [InlineData(CcSpinnerSize.Large, "cc-spinner cc-spinner--large")]
  public void Render_WithSize_ShouldApplyItsModifier(CcSpinnerSize size, string expected)
  {
    var cut = Render<CcLoadingSpinner>(p => p.Add(s => s.Size, size));

    cut.Find("[role=status]").ClassName.ShouldBe(expected);
  }

  [Fact]
  public void Render_WithHiddenLabel_ShouldKeepItForScreenReaders()
  {
    var cut = Render<CcLoadingSpinner>(p => p.Add(s => s.Label, "Loading the commits").Add(s => s.ShowLabel, false));

    var label = cut.Find(".cc-sr-only");
    label.TextContent.ShouldBe("Loading the commits");
    cut.FindAll(".cc-spinner__label").ShouldBeEmpty();
  }

  #endregion
}
