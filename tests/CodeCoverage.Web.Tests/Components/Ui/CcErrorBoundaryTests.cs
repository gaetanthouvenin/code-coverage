// ------------------------------------------------------------------------------------------------
// <copyright file="CcErrorBoundaryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcErrorBoundaryTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_WithHealthyContent_ShouldRenderTheContent()
  {
    var cut = Render<CcErrorBoundary>(p => p.AddChildContent<ThrowingComponent>());

    cut.Find(".healthy").TextContent.ShouldBe("healthy");
    cut.FindAll("[role=alert]").ShouldBeEmpty();
  }

  [Fact]
  public void Render_WhenContentThrows_ShouldShowALocalizedAlertWithRetry()
  {
    var cut = Render<CcErrorBoundary>(p => p.AddChildContent<ThrowingComponent>(c => c.Add(t => t.ShouldFail, () => true)));

    var alert = cut.Find("[role=alert]");
    alert.ClassList.ShouldContain("cc-error-boundary");
    cut.Find("h2").TextContent.ShouldBe(Text("ErrorTitle"));
    cut.Find(".cc-empty-state__description").TextContent.ShouldBe(Text("ErrorDescription"));
    cut.Find("button.cc-button--primary").TextContent.Trim().ShouldBe(Text("Retry"));
  }

  [Fact]
  public void Retry_AfterTheFaultIsGone_ShouldRecoverTheContent()
  {
    var failing = true;
    var cut = Render<CcErrorBoundary>(p => p.AddChildContent<ThrowingComponent>(c => c.Add(t => t.ShouldFail, () => failing)));
    cut.FindAll("[role=alert]").Count.ShouldBe(1);

    failing = false;
    cut.Find("button").Click();

    cut.Find(".healthy").ShouldNotBeNull();
    cut.FindAll("[role=alert]").ShouldBeEmpty();
  }

  [Fact]
  public void Render_WithErrorContent_ShouldUseIt()
  {
    var cut = Render<CcErrorBoundary>(p => p.AddChildContent<ThrowingComponent>(c => c.Add(t => t.ShouldFail, () => true))
                                            .Add(b => b.ErrorContent, (RenderFragment<Exception>)(ex => b => b.AddContent(0, ex.Message))));

    cut.Markup.ShouldContain("Rendering failed.");
    cut.FindAll(".cc-error-boundary").ShouldBeEmpty();
  }

  #endregion
}
