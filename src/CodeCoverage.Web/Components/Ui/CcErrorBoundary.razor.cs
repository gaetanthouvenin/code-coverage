// ------------------------------------------------------------------------------------------------
// <copyright file="CcErrorBoundary.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   Catches an exception thrown while rendering its content, logs it, and shows a localized
///   alert with a « Try again » button that calls <see cref="ErrorBoundaryBase.Recover" />.
/// </summary>
/// <remarks>
///   The layout keeps one instance around the page and recovers it on every navigation; a custom
///   <see cref="ErrorBoundaryBase.ErrorContent" /> replaces the default alert.
/// </remarks>
public partial class CcErrorBoundary
{
  #region Injected services

  [Inject]
  private IStringLocalizer<UiStrings> L { get; set; } = null!;

  [Inject]
  private ILogger<CcErrorBoundary> Logger { get; set; } = null!;

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override Task OnErrorAsync(Exception exception)
  {
    Logger.LogError(exception, "Unhandled exception while rendering a component");

    return Task.CompletedTask;
  }

  #endregion
}
