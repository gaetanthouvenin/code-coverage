// ------------------------------------------------------------------------------------------------
// <copyright file="RenotifyButton.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Comparisons;

/// <summary>
///   Re-notify, for the administrators of the repository only: the page sends the command and says what came of it. The
///   server checks the right again, so hiding the button is no protection in itself.
/// </summary>
public partial class RenotifyButton
{
  #region Parameters

  /// <summary>Gets or sets a value indicating whether the button is shown.</summary>
  [Parameter]
  public bool Visible { get; set; }

  /// <summary>Gets or sets a value indicating whether a re-notification runs: the button is disabled meanwhile.</summary>
  [Parameter]
  public bool Busy { get; set; }

  /// <summary>Gets or sets the callback run on a click.</summary>
  [Parameter]
  public EventCallback OnClick { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
