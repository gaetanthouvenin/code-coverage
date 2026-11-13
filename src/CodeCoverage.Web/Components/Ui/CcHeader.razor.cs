// ------------------------------------------------------------------------------------------------
// <copyright file="CcHeader.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The header of a page: an optional breadcrumb, the title (the only <c>h1</c> of the page)
///   with an optional icon, a subtitle, the actions on the right and the second-level tabs below.
/// </summary>
/// <remarks>The last step of the breadcrumb is the current page (<c>aria-current="page"</c>).</remarks>
public partial class CcHeader
{
  #region Parameters

  /// <summary>Gets or sets the localized title of the page.</summary>
  [Parameter, EditorRequired]
  public string Title { get; set; } = null!;

  /// <summary>Gets or sets the icon shown before the title, if any.</summary>
  [Parameter]
  public CcIconName? Icon { get; set; }

  /// <summary>Gets or sets the localized subtitle shown under the title.</summary>
  [Parameter]
  public string? Subtitle { get; set; }

  /// <summary>
  ///   Gets or sets the line under the title when it holds more than text, a link or a code span; it wins over
  ///   <see cref="Subtitle" />.
  /// </summary>
  [Parameter]
  public RenderFragment? SubtitleContent { get; set; }

  /// <summary>Gets or sets the steps of the breadcrumb, the current page last.</summary>
  [Parameter]
  public IReadOnlyList<CcBreadcrumbItem>? Breadcrumb { get; set; }

  /// <summary>Gets or sets the actions of the page (pill buttons, selects, search).</summary>
  [Parameter]
  public RenderFragment? Actions { get; set; }

  /// <summary>Gets or sets the second-level tabs shown under the header (a <see cref="CcPillTabs" />).</summary>
  [Parameter]
  public RenderFragment? Tabs { get; set; }

  /// <summary>Gets or sets the extra CSS classes of the header.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<UiStrings> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the CSS classes of the header.</summary>
  private string CssClass => "cc-header" + CcCss.Extra(Class);

  #endregion
}
