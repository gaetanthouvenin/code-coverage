// ------------------------------------------------------------------------------------------------
// <copyright file="DiffTable.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Components.Comparisons;

/// <summary>The <c>diff</c> table of a comparison, with the rows and the signs of the comment of the pull request.</summary>
public partial class DiffTable
{
  #region Parameters

  /// <summary>Gets or sets the lines of the table.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<DiffLineViewModel> Lines { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Methods

  private static string? ClassOf(DiffLineKind kind) => kind switch
  {
    DiffLineKind.Head => "cc-code__head",
    DiffLineKind.Add => "cc-code__add",
    DiffLineKind.Delete => "cc-code__del",
    var _ => null,
  };

  #endregion
}
