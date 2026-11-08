// ------------------------------------------------------------------------------------------------
// <copyright file="CcLoadingSpinner.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>A spinning ring with its label, announced as a status while content loads.</summary>
/// <remarks>
///   With <see cref="ShowLabel" /> set to <see langword="false" />, the label stays readable by
///   screen readers. The ring slows down when the user prefers reduced motion.
/// </remarks>
public partial class CcLoadingSpinner
{
  #region Parameters

  /// <summary>Gets or sets the label; <see langword="null" /> uses the localized « Loading… ».</summary>
  [Parameter]
  public string? Label { get; set; }

  /// <summary>Gets or sets the size of the ring. Defaults to <see cref="CcSpinnerSize.Medium" />.</summary>
  [Parameter]
  public CcSpinnerSize Size { get; set; } = CcSpinnerSize.Medium;

  /// <summary>Gets or sets a value indicating whether the label is visible. Defaults to <see langword="true" />.</summary>
  [Parameter]
  public bool ShowLabel { get; set; } = true;

  /// <summary>Gets or sets the extra CSS classes of the spinner.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<UiStrings> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the label to render.</summary>
  private string EffectiveLabel => string.IsNullOrEmpty(Label) ? L["Loading"] : Label;

  /// <summary>Gets the CSS classes of the spinner.</summary>
  private string CssClass
  {
    get
    {
      var size = Size switch
      {
        CcSpinnerSize.Small => " cc-spinner--small",
        CcSpinnerSize.Large => " cc-spinner--large",
        var _ => string.Empty,
      };

      return "cc-spinner" + size + CcCss.Extra(Class);
    }
  }

  #endregion
}
