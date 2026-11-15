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
///   The page the exception handler re-executes after an unhandled error, and the status code pages after an error
///   status answered without a body, a 404 aside. It shows the request identifier, which leads to the error in the
///   logs, never the error itself.
/// </summary>
/// <remarks>
///   The page renders on the server alone and reads nothing from the database: it must still answer when the database
///   is down, for a signed-in user too, whose session is not checked on this address (<c>SessionCookieEvents</c>).
/// </remarks>
public partial class Error
{
  #region Parameters

  /// <summary>Gets or sets the error status the status code pages re-executed the page for, if any.</summary>
  [Parameter]
  public int? StatusCode { get; set; }

  [CascadingParameter]
  private HttpContext? HttpContext { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the failed request, as the logs record it.</summary>
  private string? RequestId { get; set; }

  #endregion

  #region Lifecycle

  /// <inheritdoc />
  protected override void OnInitialized() =>
    RequestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;

  #endregion
}
