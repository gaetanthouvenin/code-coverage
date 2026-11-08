// ------------------------------------------------------------------------------------------------
// <copyright file="CcPageContainer.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The column of a page: the header, then the cards, stacked with the vertical rhythm of the
///   mockup (32 px, 22 px on a phone).
/// </summary>
public partial class CcPageContainer
{
  #region Parameters

  /// <summary>Gets or sets the content of the page.</summary>
  [Parameter]
  public RenderFragment? ChildContent { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the column.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  /// <summary>Gets the CSS classes of the column.</summary>
  private string CssClass => "cc-page" + CcCss.Extra(Class);

  #endregion
}
