// ------------------------------------------------------------------------------------------------
// <copyright file="CcButton.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   A rounded button, or a link drawn as a button when <see cref="Href" /> is set, with an
///   optional leading icon.
/// </summary>
/// <remarks>
///   The pill variants (<see cref="CcButtonVariant.Pill" />, <see cref="CcButtonVariant.PillAccent" />)
///   draw their icon in a round, as the actions of the page headers of the mockup. An icon-only
///   button needs <see cref="AriaLabel" />.
/// </remarks>
public partial class CcButton
{
  #region Parameters

  /// <summary>Gets or sets the label of the button.</summary>
  [Parameter]
  public RenderFragment? ChildContent { get; set; }

  /// <summary>Gets or sets the look of the button. Defaults to <see cref="CcButtonVariant.Secondary" />.</summary>
  [Parameter]
  public CcButtonVariant Variant { get; set; } = CcButtonVariant.Secondary;

  /// <summary>Gets or sets the height of the button. Defaults to <see cref="CcButtonSize.Medium" />.</summary>
  [Parameter]
  public CcButtonSize Size { get; set; } = CcButtonSize.Medium;

  /// <summary>Gets or sets the icon shown before the label, if any.</summary>
  [Parameter]
  public CcIconName? Icon { get; set; }

  /// <summary>Gets or sets the native <c>type</c> of the button. Defaults to <c>button</c>.</summary>
  [Parameter]
  public string Type { get; set; } = "button";

  /// <summary>Gets or sets the address of the link; when set, an <c>a</c> element is rendered.</summary>
  [Parameter]
  public string? Href { get; set; }

  /// <summary>Gets or sets a value indicating whether the button is disabled.</summary>
  [Parameter]
  public bool Disabled { get; set; }

  /// <summary>Gets or sets the accessible name, required when the button shows an icon only.</summary>
  [Parameter]
  public string? AriaLabel { get; set; }

  /// <summary>Gets or sets the callback raised when the button is clicked.</summary>
  [Parameter]
  public EventCallback<MouseEventArgs> OnClick { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the button.</summary>
  [Parameter]
  public string? Class { get; set; }

  /// <summary>Gets or sets the other attributes, copied on the element (<c>title</c>, <c>data-*</c>…).</summary>
  [Parameter(CaptureUnmatchedValues = true)]
  public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

  #endregion

  #region Properties

  /// <summary>Gets a value indicating whether the variant draws its icon in a round.</summary>
  private bool IsPill => Variant is CcButtonVariant.Pill or CcButtonVariant.PillAccent;

  /// <summary>Gets the CSS classes of the element.</summary>
  private string CssClass
  {
    get
    {
      var variant = Variant switch
      {
        CcButtonVariant.Primary => " cc-button--primary",
        CcButtonVariant.Danger => " cc-button--danger",
        CcButtonVariant.SolidDanger => " cc-button--solid-danger",
        CcButtonVariant.Pill => " cc-button--pill",
        CcButtonVariant.PillAccent => " cc-button--pill-accent",
        var _ => string.Empty,
      };
      var size = Size == CcButtonSize.Small && !IsPill ? " cc-button--small" : string.Empty;

      return "cc-button" + variant + size + CcCss.Extra(Class);
    }
  }

  #endregion
}
