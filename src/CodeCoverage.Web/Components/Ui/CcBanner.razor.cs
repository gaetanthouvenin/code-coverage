// ------------------------------------------------------------------------------------------------
// <copyright file="CcBanner.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   An inline message tinted by its tone, with an icon, an optional bold title, a body and
///   optional actions.
/// </summary>
/// <remarks>
///   A <see cref="CcTone.Warning" /> or <see cref="CcTone.Danger" /> banner is <c>role="alert"</c>
///   (announced at once); the other tones are <c>role="status"</c>.
/// </remarks>
public partial class CcBanner
{
  #region Parameters

  /// <summary>Gets or sets the body of the banner.</summary>
  [Parameter]
  public RenderFragment? ChildContent { get; set; }

  /// <summary>Gets or sets the semantic colour. Defaults to <see cref="CcTone.Info" />.</summary>
  [Parameter]
  public CcTone Tone { get; set; } = CcTone.Info;

  /// <summary>Gets or sets the optional bold first line.</summary>
  [Parameter]
  public string? Title { get; set; }

  /// <summary>Gets or sets the icon; <see langword="null" /> uses the icon of the tone.</summary>
  [Parameter]
  public CcIconName? Icon { get; set; }

  /// <summary>Gets or sets a value indicating whether the icon is shown. Defaults to <see langword="true" />.</summary>
  [Parameter]
  public bool ShowIcon { get; set; } = true;

  /// <summary>Gets or sets the optional actions (buttons, links) shown on the right.</summary>
  [Parameter]
  public RenderFragment? Actions { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the banner.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  /// <summary>Gets the ARIA live-region role of the banner.</summary>
  private string Role => Tone is CcTone.Warning or CcTone.Danger ? "alert" : "status";

  /// <summary>Gets the icon to draw, if any.</summary>
  private CcIconName? EffectiveIcon => ShowIcon ? Icon ?? DefaultIcon(Tone) : null;

  /// <summary>Gets the CSS classes of the banner.</summary>
  private string CssClass => $"cc-banner cc-banner--{CcCss.Tone(Tone)}{CcCss.Extra(Class)}";

  #endregion

  #region Methods

  /// <summary>Gets the default icon of a tone.</summary>
  /// <param name="tone">The tone of the banner.</param>
  /// <returns>
  ///   <see cref="CcIconName.Check" /> for a success, <see cref="CcIconName.Alert" /> for a warning
  ///   or a danger, <see cref="CcIconName.Info" /> otherwise.
  /// </returns>
  public static CcIconName DefaultIcon(CcTone tone) =>
    tone switch
    {
      CcTone.Success => CcIconName.Check,
      CcTone.Warning or CcTone.Danger => CcIconName.Alert,
      var _ => CcIconName.Info,
    };

  #endregion
}
