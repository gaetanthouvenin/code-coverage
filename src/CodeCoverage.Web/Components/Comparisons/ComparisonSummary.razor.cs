// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonSummary.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Components.Comparisons;

/// <summary>
///   The indicators of a comparison: the project coverage against its base, the patch coverage, the indirect changes
///   and the changed lines missed, then the gauge of the patch. No patch target is shown: nothing stores it.
/// </summary>
public partial class ComparisonSummary
{
  #region Parameters

  /// <summary>Gets or sets the comparison.</summary>
  [Parameter, EditorRequired]
  public ComparisonViewModel Comparison { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
