// ------------------------------------------------------------------------------------------------
// <copyright file="AccessDeniedTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

public sealed class AccessDeniedTests : UiTestContext
{
  #region Properties

  private IStringLocalizer<Common> L => Services.GetRequiredService<IStringLocalizer<Common>>();

  #endregion

  #region Methods

  // A sign-in refused for want of a repository says why, and leaves no session behind.
  [Fact]
  public void Render_NoRepositoryReason_ShouldExplainTheRefusedSignIn()
  {
    Navigation.NavigateTo($"access-denied?reason={AccessDeniedReasons.NoRepository}");

    var cut = Render<AccessDenied>();

    cut.Markup.ShouldContain(L["Access.NoRepository"].Value);
  }

  [Fact]
  public void Render_WithoutReason_ShouldShowTheGenericExplanation()
  {
    var cut = Render<AccessDenied>();

    cut.Markup.ShouldContain(L["AccessDenied_Description"].Value);
  }

  #endregion
}
