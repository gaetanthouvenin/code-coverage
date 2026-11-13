// ------------------------------------------------------------------------------------------------
// <copyright file="IssuedTokenDialogTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Tokens;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;
using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.Tests.Components.Tokens;

// The dialog that shows the value of a token once (M3.35).
public sealed class IssuedTokenDialogTests : UiTestContext
{
  #region Fields

  private const string Value = "d3f1a9e2-7c44-4b7e-9a31-6f1c04a7b2e8";

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowTheValueOnceWithItsWarning()
  {
    var cut = Render<IssuedTokenDialog>(p => p.Add(static c => c.Token, new IssuedTokenViewModel("azure-pipelines", Value)));

    cut.Find("pre.cc-code").TextContent.ShouldBe(Value);
    cut.Markup.ShouldContain(L("Token_CopyItNow"));
  }

  // The value lives in the dialog only: closing it tells the page to forget it, and nothing shows it again.
  [Fact]
  public void Close_ShouldForgetValue()
  {
    var closed = 0;
    var cut = Render<IssuedTokenDialog>(p => p.Add(static c => c.Token, new IssuedTokenViewModel("azure-pipelines", Value))
                                              .Add(static c => c.OnClosed, EventCallback.Factory.Create(this, () => closed++)));

    cut.FindAll(".cc-dialog__actions button").Last().Click();

    closed.ShouldBe(1);
    cut.Render(p => p.Add(static c => c.Token, (IssuedTokenViewModel?)null));
    cut.Markup.ShouldNotContain(Value);
  }

  // A view model that holds a value never writes it down: a log, an exception message, anything.
  [Fact]
  public void ToString_ShouldNotCarryTheValue() =>
    new IssuedTokenViewModel("azure-pipelines", Value).ToString().ShouldNotContain(Value);

  [Fact]
  public void Render_WithoutToken_ShouldShowNothing()
  {
    var cut = Render<IssuedTokenDialog>(p => p.Add(static c => c.Token, (IssuedTokenViewModel?)null));

    cut.FindAll(".cc-dialog").ShouldBeEmpty();
  }

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}
