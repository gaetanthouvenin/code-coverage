// ------------------------------------------------------------------------------------------------
// <copyright file="CcEmptyState.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui.Icons;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The centred placeholder of an empty list or of a missing resource: an icon in a gradient
///   round, a title, a description and optional actions.
/// </summary>
public partial class CcEmptyState
{
  #region Parameters

  /// <summary>Gets or sets the localized title.</summary>
  [Parameter, EditorRequired]
  public string Title { get; set; } = null!;

  /// <summary>Gets or sets the icon. Defaults to <see cref="CcIconName.Search" />.</summary>
  [Parameter]
  public CcIconName Icon { get; set; } = CcIconName.Search;

  /// <summary>Gets or sets the description shown under the title.</summary>
  [Parameter]
  public RenderFragment? ChildContent { get; set; }

  /// <summary>Gets or sets the optional actions shown under the description.</summary>
  [Parameter]
  public RenderFragment? Actions { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the placeholder.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  /// <summary>Gets the CSS classes of the placeholder.</summary>
  private string CssClass => "cc-empty-state" + CcCss.Extra(Class);

  #endregion
}
