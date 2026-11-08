// ------------------------------------------------------------------------------------------------
// <copyright file="Error.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Pages;

/// <summary>
///   The page the exception handler re-executes after an unhandled error. It shows the request
///   identifier, which leads to the error in the logs, never the error itself.
/// </summary>
public partial class Error
{
  #region Parameters

  [CascadingParameter]
  private HttpContext? HttpContext { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets or sets the identifier of the failed request, as the logs record it.</summary>
  /// <remarks>
  ///   Read while the page is prerendered, when the request is known, and persisted into the
  ///   interactive render, where it is not.
  /// </remarks>
  [PersistentState]
  public string? RequestId { get; set; }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized() =>
    RequestId ??= Activity.Current?.Id ?? HttpContext?.TraceIdentifier;

  #endregion
}
