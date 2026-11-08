// ------------------------------------------------------------------------------------------------
// <copyright file="CcIcon.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace CodeCoverage.Web.Components.Ui.Icons;

/// <summary>An inline SVG icon of the Coverage set (24 px grid, 2 px stroke, current colour).</summary>
/// <remarks>
///   Without <see cref="Title" /> the icon is decorative and hidden from assistive technologies;
///   with it, the icon is an image named by the title.
/// </remarks>
public partial class CcIcon
{
  #region Parameters

  /// <summary>Gets or sets the icon to draw.</summary>
  [Parameter, EditorRequired]
  public CcIconName Name { get; set; }

  /// <summary>Gets or sets the accessible name of a meaningful icon; omit it for a decorative one.</summary>
  [Parameter]
  public string? Title { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the <c>svg</c> element.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Properties

  /// <summary>Gets a value indicating whether the icon is decorative (no <see cref="Title" />).</summary>
  private bool IsDecorative => string.IsNullOrEmpty(Title);

  /// <summary>Gets the CSS classes of the <c>svg</c> element.</summary>
  private string CssClass => string.IsNullOrEmpty(Class) ? "cc-icon" : $"cc-icon {Class}";

  /// <summary>Gets the SVG elements of the icon.</summary>
  private MarkupString Markup => new(CcIconPaths.Get(Name));

  #endregion
}
