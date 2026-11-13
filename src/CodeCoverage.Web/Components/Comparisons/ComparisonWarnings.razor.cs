// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonWarnings.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Components.Comparisons;

/// <summary>The warnings of a comparison: a diff the forge truncated, and a base without a report.</summary>
public partial class ComparisonWarnings
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
