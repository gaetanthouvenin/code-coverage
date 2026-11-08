// ------------------------------------------------------------------------------------------------
// <copyright file="CcSearchBar.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>A pill search input with its magnifier, in a <c>search</c> landmark.</summary>
/// <remarks>
///   The placeholder defaults to the localized « Search », and names the input unless
///   <see cref="AriaLabel" /> is set.
/// </remarks>
public partial class CcSearchBar
{
  #region Parameters

  /// <summary>Gets or sets the searched text (supports <c>@bind-Value</c>).</summary>
  [Parameter]
  public string? Value { get; set; }

  /// <summary>Gets or sets the callback raised on every keystroke with the new text.</summary>
  [Parameter]
  public EventCallback<string?> ValueChanged { get; set; }

  /// <summary>Gets or sets the localized placeholder; <see langword="null" /> uses « Search ».</summary>
  [Parameter]
  public string? Placeholder { get; set; }

  /// <summary>Gets or sets the accessible name of the input; <see langword="null" /> uses the placeholder.</summary>
  [Parameter]
  public string? AriaLabel { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the search bar.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<UiStrings> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the placeholder to render.</summary>
  private string EffectivePlaceholder => string.IsNullOrEmpty(Placeholder) ? L["Search"] : Placeholder;

  /// <summary>Gets the accessible name of the input.</summary>
  private string EffectiveAriaLabel => string.IsNullOrEmpty(AriaLabel) ? EffectivePlaceholder : AriaLabel;

  /// <summary>Gets the CSS classes of the search bar.</summary>
  private string CssClass => "cc-search" + CcCss.Extra(Class);

  #endregion

  #region Methods

  private Task OnInputAsync(ChangeEventArgs args) =>
    ValueChanged.InvokeAsync(args.Value?.ToString());

  #endregion
}
