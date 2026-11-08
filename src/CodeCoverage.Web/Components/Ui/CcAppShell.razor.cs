// ------------------------------------------------------------------------------------------------
// <copyright file="CcAppShell.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The chrome of the application: a soft gray ground, a centred frame of at most 1360 px, the
///   sticky top bar (no sidebar), then the <c>main</c> landmark with the routed content.
/// </summary>
/// <remarks>
///   A « Skip to content » link, visible on focus only, is the first tab stop and moves the focus
///   to the <c>main</c> landmark.
/// </remarks>
public partial class CcAppShell
{
  #region Parameters

  /// <summary>Gets or sets the top bar (a <see cref="CcTopNav" />).</summary>
  [Parameter]
  public RenderFragment? TopNav { get; set; }

  /// <summary>Gets or sets the routed content.</summary>
  [Parameter]
  public RenderFragment? ChildContent { get; set; }

  /// <summary>Gets or sets the identifier of the <c>main</c> landmark. Defaults to <c>cc-main</c>.</summary>
  [Parameter]
  public string MainId { get; set; } = "cc-main";

  /// <summary>Gets or sets the extra CSS classes of the shell.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private IStringLocalizer<UiStrings> L { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the address of the skip link: the current page, anchored on the <c>main</c> landmark.</summary>
  /// <remarks>The base address of the page makes a bare fragment point at the home page.</remarks>
  private string SkipHref
  {
    get
    {
      var relative = Navigation.ToBaseRelativePath(Navigation.Uri);
      var end = relative.IndexOf('#', StringComparison.Ordinal);

      return (end < 0 ? relative : relative[..end]) + "#" + MainId;
    }
  }

  /// <summary>Gets the CSS classes of the shell.</summary>
  private string CssClass => "cc-shell" + CcCss.Extra(Class);

  #endregion
}
