// ------------------------------------------------------------------------------------------------
// <copyright file="CcToastTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcToastTests : UiTestContext
{
  #region Properties

  private IToastService Toasts => Services.GetRequiredService<IToastService>();

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldBeAPoliteLiveRegion()
  {
    var cut = Render<CcToast>();

    var region = cut.Find("section.cc-toasts");
    region.GetAttribute("aria-live").ShouldBe("polite");
    region.GetAttribute("aria-label").ShouldBe(Text("Notifications"));
    cut.FindAll(".cc-toast").ShouldBeEmpty();
  }

  [Theory]
  [InlineData(CcTone.Danger, "alert")]
  [InlineData(CcTone.Warning, "alert")]
  [InlineData(CcTone.Success, "status")]
  [InlineData(CcTone.Info, "status")]
  public void Show_WithTone_ShouldRenderTheToastWithTheMatchingRole(CcTone tone, string role)
  {
    var cut = Render<CcToast>();

    Toasts.Show(tone, "Token revoked", "ci-main can no longer be used.");

    cut.WaitForAssertion(() =>
    {
      var toast = cut.Find(".cc-toast");
      toast.GetAttribute("role").ShouldBe(role);
      toast.ClassList.ShouldContain($"cc-toast--{tone.ToString().ToLowerInvariant()}");
      cut.Find(".cc-toast__title").TextContent.ShouldBe("Token revoked");
      cut.Find(".cc-toast__message").TextContent.ShouldBe("ci-main can no longer be used.");
    });
  }

  [Fact]
  public void Show_WithoutMessage_ShouldRenderTheTitleOnly()
  {
    var cut = Render<CcToast>();

    Toasts.Show(CcTone.Success, "Saved");

    cut.WaitForAssertion(() => cut.Find(".cc-toast__title").TextContent.ShouldBe("Saved"));
    cut.FindAll(".cc-toast__message").ShouldBeEmpty();
  }

  [Fact]
  public void Click_Close_ShouldDismissTheToast()
  {
    var cut = Render<CcToast>();
    Toasts.Show(CcTone.Danger, "Failed");
    cut.WaitForAssertion(() => cut.FindAll(".cc-toast").Count.ShouldBe(1));

    var close = cut.Find(".cc-toast__close");
    close.GetAttribute("aria-label").ShouldBe(Text("Dismiss"));
    close.Click();

    cut.WaitForAssertion(() => cut.FindAll(".cc-toast").ShouldBeEmpty());
    Toasts.Toasts.ShouldBeEmpty();
  }

  [Fact]
  public void Toast_AfterTheDuration_ShouldBeDismissed()
  {
    var cut = Render<CcToast>(p => p.Add(t => t.Duration, TimeSpan.FromMilliseconds(20)));

    Toasts.Show(CcTone.Info, "Copied");

    cut.WaitForAssertion(() => Toasts.Toasts.ShouldBeEmpty(), TimeSpan.FromSeconds(5));
    cut.WaitForAssertion(() => cut.FindAll(".cc-toast").ShouldBeEmpty());
  }

  [Fact]
  public void Render_WithToastShownBefore_ShouldShowIt()
  {
    Toasts.Show(CcTone.Success, "Shown before the stack");

    var cut = Render<CcToast>();

    cut.Find(".cc-toast__title").TextContent.ShouldBe("Shown before the stack");
  }

  #endregion
}
