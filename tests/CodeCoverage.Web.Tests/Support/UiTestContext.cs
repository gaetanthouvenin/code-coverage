// ------------------------------------------------------------------------------------------------
// <copyright file="UiTestContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Extensions;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Tests.Support;

// A bUnit context with the services of the UI components, a loose JS runtime and helpers to read
// the localized strings in the culture of the run.
public abstract class UiTestContext : BunitContext
{
  #region Ctors

  protected UiTestContext()
  {
    Services.AddLogging();
    Services.AddCoverageUi();
    JSInterop.Mode = JSRuntimeMode.Loose;
  }

  #endregion

  #region Properties

  protected NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

  #endregion

  #region Methods

  protected string Text(string key) =>
    Services.GetRequiredService<IStringLocalizer<UiStrings>>()[key];

  #endregion
}
