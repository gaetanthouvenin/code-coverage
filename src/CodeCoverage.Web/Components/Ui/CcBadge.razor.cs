// ------------------------------------------------------------------------------------------------
// <copyright file="CcBadge.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   A small rounded label tinted by its tone (a state, a coverage level), or, with
///   <see cref="Chip" />, the compact chip of a delta next to a value.
/// </summary>
public partial class CcBadge
{
  #region Parameters

  /// <summary>Gets or sets the text of the badge.</summary>
  [Parameter]
  public RenderFragment? ChildContent { get; set; }

  /// <summary>Gets or sets the semantic colour. Defaults to <see cref="CcTone.Neutral" />.</summary>
  [Parameter]
  public CcTone Tone { get; set; } = CcTone.Neutral;

  /// <summary>Gets or sets a value indicating whether the badge is drawn as a compact chip.</summary>
  [Parameter]
  public bool Chip { get; set; }

  /// <summary>Gets or sets the icon shown before the text, if any.</summary>
  [Parameter]
  public CcIconName? Icon { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the badge.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  /// <summary>Gets the CSS classes of the badge.</summary>
  private string CssClass =>
    $"cc-badge cc-badge--{CcCss.Tone(Tone)}{(Chip ? " cc-badge--chip" : string.Empty)}{CcCss.Extra(Class)}";

  #endregion
}
