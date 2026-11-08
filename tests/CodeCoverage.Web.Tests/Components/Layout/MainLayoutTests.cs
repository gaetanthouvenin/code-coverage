// ------------------------------------------------------------------------------------------------
// <copyright file="MainLayoutTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Layout;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Layout;

public sealed class MainLayoutTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_ShouldShowTheShellWithTheNavigationAndThePage()
  {
    var cut = Render<MainLayout>(p => p.Add(l => l.Body, Page(() => false)));

    cut.Find(".cc-shell .cc-topnav").ShouldNotBeNull();
    cut.Find("main .healthy").TextContent.ShouldBe("healthy");
    cut.FindAll(".cc-topnav__tabs a")
       .Select(a => a.TextContent)
       .ShouldBe([Services.GetRequiredService<IStringLocalizer<Common>>()["Nav_Repositories"].Value]);
  }

  [Fact]
  public void Render_ShouldHostTheToastStack()
  {
    var cut = Render<MainLayout>(p => p.Add(l => l.Body, Page(() => false)));

    cut.FindAll("section.cc-toasts").Count.ShouldBe(1);
  }

  [Fact]
  public void NavigationChange_AfterError_ShouldRecoverBoundary()
  {
    var failing = true;
    var cut = Render<MainLayout>(p => p.Add(l => l.Body, Page(() => failing)));
    cut.FindAll(".cc-error-boundary[role=alert]").Count.ShouldBe(1);

    failing = false;
    Navigation.NavigateTo("other-page");

    cut.WaitForAssertion(() =>
    {
      cut.Find("main .healthy").ShouldNotBeNull();
      cut.FindAll(".cc-error-boundary").ShouldBeEmpty();
    });
  }

  private static RenderFragment Page(Func<bool> shouldFail) => builder =>
  {
    builder.OpenComponent<ThrowingComponent>(0);
    builder.AddComponentParameter(1, nameof(ThrowingComponent.ShouldFail), shouldFail);
    builder.CloseComponent();
  };

  #endregion
}
