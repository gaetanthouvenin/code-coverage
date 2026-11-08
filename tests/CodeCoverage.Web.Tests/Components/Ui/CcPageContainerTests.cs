// ------------------------------------------------------------------------------------------------
// <copyright file="CcPageContainerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcPageContainerTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_ShouldStackItsContentInThePageColumn()
  {
    var cut = Render<CcPageContainer>(p => p.AddChildContent("<section>a</section>").Add(c => c.Class, "extra"));

    var page = cut.Find("div.cc-page");
    page.ClassName.ShouldBe("cc-page extra");
    page.QuerySelector("section")!.TextContent.ShouldBe("a");
  }

  #endregion
}
