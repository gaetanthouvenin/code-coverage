// ------------------------------------------------------------------------------------------------
// <copyright file="IndirectChanges.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Components.Comparisons;

/// <summary>
///   The files whose coverage moved outside the diff, all of them, 50 a page: their coverage at the head, read from the
///   head report, and their change.
/// </summary>
public partial class IndirectChanges
{
  #region Fields

  /// <summary>The rows of a page.</summary>
  internal const int PageSize = 50;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the changes, in path order.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<IndirectChangeViewModel> Changes { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private int Page { get; set; } = 1;

  private IEnumerable<IndirectChangeViewModel> Shown => Changes.Skip((Page - 1) * PageSize).Take(PageSize);

  #endregion

  #region Methods

  private static string DeltaClass(int trend) => trend switch
  {
    > 0 => "cc-delta--up",
    < 0 => "cc-delta--down",
    var _ => "cc-dim",
  };

  #endregion
}
