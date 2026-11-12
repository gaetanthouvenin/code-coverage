// ------------------------------------------------------------------------------------------------
// <copyright file="CcDialogTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcDialogTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_Open_ShouldBeAModalNamedByItsTitle()
  {
    var cut = Render<CcDialog>(p => p.Add(d => d.Open, true)
                                     .Add(d => d.Title, "Replace")
                                     .Add(d => d.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<p>body</p>")))
                                     .Add(d => d.Actions, (RenderFragment)(b => b.AddMarkupContent(0, "<button>ok</button>"))));

    var dialog = cut.Find("[role=dialog]");
    dialog.GetAttribute("aria-modal").ShouldBe("true");
    cut.Find($"#{dialog.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Replace");
    cut.Find(".cc-dialog__actions button").TextContent.ShouldBe("ok");
  }

  // Review R7: opened, the dialog takes the focus (and keeps it inside); closed, it gives it back to the opener.
  [Fact]
  public void Open_ShouldTakeTheFocusAndGiveItBackOnClose()
  {
    var cut = Render<CcDialog>(p => p.Add(d => d.Open, true).Add(d => d.Title, "Replace"));

    cut.Find("[role=dialog]").GetAttribute("tabindex").ShouldBe("-1");
    JSInterop.VerifyInvoke("ccUi.openDialog");

    cut.Render(p => p.Add(d => d.Open, false));

    JSInterop.VerifyInvoke("ccUi.closeDialog");
  }

  [Fact]
  public void Render_Closed_ShouldRenderNothing() =>
    Render<CcDialog>(p => p.Add(d => d.Title, "Replace")).Markup.Trim().ShouldBeEmpty();

  [Fact]
  public void Escape_ShouldClose()
  {
    var closed = false;
    var cut = Render<CcDialog>(p => p.Add(d => d.Open, true).Add(d => d.Title, "Replace").Add(d => d.OnClose, () => closed = true));

    cut.Find("[role=dialog]").KeyDown("Escape");

    closed.ShouldBeTrue();
  }

  [Fact]
  public void ClickOnTheScrim_ShouldClose()
  {
    var closed = false;
    var cut = Render<CcDialog>(p => p.Add(d => d.Open, true).Add(d => d.Title, "Replace").Add(d => d.OnClose, () => closed = true));

    cut.Find(".cc-dialog__scrim").Click();

    closed.ShouldBeTrue();
  }

  #endregion
}
