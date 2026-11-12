// ------------------------------------------------------------------------------------------------
// <copyright file="CcTable.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>A table of rows in a card, scrolled sideways on a narrow screen.</summary>
/// <remarks>Its cells take <c>cc-r</c> (right-aligned), <c>cc-num</c>, <c>cc-muted</c> and <c>cc-nowrap</c>.</remarks>
public partial class CcTable
{
  #region Parameters

  /// <summary>Gets or sets the header cells (<c>th</c>).</summary>
  [Parameter]
  public RenderFragment? Head { get; set; }

  /// <summary>Gets or sets the rows (<c>tr</c>).</summary>
  [Parameter]
  public RenderFragment? ChildContent { get; set; }

  /// <summary>Gets or sets the accessible name of the table.</summary>
  [Parameter]
  public string? AriaLabel { get; set; }

  /// <summary>Gets or sets extra CSS classes of the wrapper.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  private string CssClass => "cc-table-wrap" + CcCss.Extra(Class);

  #endregion
}
