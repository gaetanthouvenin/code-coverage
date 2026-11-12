// ------------------------------------------------------------------------------------------------
// <copyright file="CcPager.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Ui;

/// <summary>
///   The foot of a paged panel (<c>cc-panel__foot</c>): the rows shown out of the total, and the previous and next pages.
/// </summary>
public partial class CcPager
{
  #region Parameters

  /// <summary>Gets or sets the number of the page shown, from 1.</summary>
  [Parameter, EditorRequired]
  public int Page { get; set; } = 1;

  /// <summary>Gets or sets the number of rows a page holds at most.</summary>
  [Parameter, EditorRequired]
  public int PageSize { get; set; }

  /// <summary>Gets or sets the number of rows of every page.</summary>
  [Parameter, EditorRequired]
  public int TotalCount { get; set; }

  /// <summary>Gets or sets the callback that receives the number of the page asked.</summary>
  [Parameter]
  public EventCallback<int> PageChanged { get; set; }

  /// <summary>Gets or sets additional CSS classes.</summary>
  [Parameter]
  public string? Class { get; set; }

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<UiStrings> L { get; set; } = null!;

  #endregion

  #region Properties

  private string CssClass => "cc-panel__foot" + CcCss.Extra(Class);

  private bool HasPrevious => Page > 1;

  private bool HasNext => (long)Page * PageSize < TotalCount;

  // « 26 – 50 of 312 »; « 0 of 0 » for an empty list.
  private string RangeText
  {
    get
    {
      var culture = CultureInfo.CurrentCulture;

      if (TotalCount <= 0 || PageSize <= 0)
      {
        return L["PagerEmpty"];
      }

      var first = Math.Min(((long)(Page - 1) * PageSize) + 1, TotalCount);
      var last = Math.Min((long)Page * PageSize, TotalCount);

      return L["PagerRange", first.ToString("N0", culture), last.ToString("N0", culture), TotalCount.ToString("N0", culture)];
    }
  }

  #endregion

  #region Methods

  /// <summary>Gets the last page of a list: the one a page past it is brought back to.</summary>
  /// <param name="totalCount">The number of items of the list.</param>
  /// <param name="pageSize">The number of items of a page.</param>
  /// <returns>The last page, from 1; 1 for an empty list.</returns>
  internal static int LastPage(int totalCount, int pageSize) =>
    totalCount <= 0 || pageSize <= 0 ? 1 : (int)(((long)totalCount + pageSize - 1) / pageSize);

  private Task PreviousAsync() => HasPrevious ? PageChanged.InvokeAsync(Page - 1) : Task.CompletedTask;

  private Task NextAsync() => HasNext ? PageChanged.InvokeAsync(Page + 1) : Task.CompletedTask;

  #endregion
}
