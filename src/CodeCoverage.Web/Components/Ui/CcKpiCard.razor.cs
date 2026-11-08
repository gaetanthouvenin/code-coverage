// ------------------------------------------------------------------------------------------------
// <copyright file="CcKpiCard.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   A key figure in a large rounded card: the label, a round icon in the corner, the value with
///   an optional trend chip, and a footer line.
/// </summary>
/// <remarks>
///   The first card of a row is the <see cref="Accent" /> one, in the blue gradient. The card is a
///   group named by its label.
/// </remarks>
public partial class CcKpiCard
{
  #region Parameters

  /// <summary>Gets or sets the localized label of the figure.</summary>
  [Parameter, EditorRequired]
  public string Label { get; set; } = null!;

  /// <summary>Gets or sets the formatted value (« 82.4 % »).</summary>
  [Parameter, EditorRequired]
  public string Value { get; set; } = null!;

  /// <summary>Gets or sets the icon of the corner round, if any.</summary>
  [Parameter]
  public CcIconName? Icon { get; set; }

  /// <summary>Gets or sets the background of the corner round. Defaults to <see cref="CcKpiIconStyle.Soft" />.</summary>
  [Parameter]
  public CcKpiIconStyle IconStyle { get; set; } = CcKpiIconStyle.Soft;

  /// <summary>Gets or sets a value indicating whether the card is drawn in the blue gradient.</summary>
  [Parameter]
  public bool Accent { get; set; }

  /// <summary>Gets or sets the chip shown next to the value (a <see cref="CcBadge" /> with <c>Chip</c>).</summary>
  [Parameter]
  public RenderFragment? Trend { get; set; }

  /// <summary>Gets or sets the footer line.</summary>
  [Parameter]
  public RenderFragment? Footer { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the card.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  /// <summary>Gets the CSS classes of the corner round.</summary>
  private string IconClass =>
    IconStyle switch
    {
      CcKpiIconStyle.Ink => "cc-kpi__icon cc-kpi__icon--ink",
      CcKpiIconStyle.Gradient => "cc-kpi__icon cc-kpi__icon--gradient",
      var _ => "cc-kpi__icon",
    };

  /// <summary>Gets the CSS classes of the card.</summary>
  private string CssClass => "cc-kpi" + (Accent ? " cc-kpi--accent" : string.Empty) + CcCss.Extra(Class);

  #endregion
}
