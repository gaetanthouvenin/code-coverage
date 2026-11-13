// ------------------------------------------------------------------------------------------------
// <copyright file="FlagComparisons.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Components.Comparisons;

/// <summary>The flags of a comparison, every one of them; a carried flag shows its badge and a dash for its change.</summary>
public partial class FlagComparisons
{
  #region Parameters

  /// <summary>Gets or sets the flags.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<FlagComparisonRowViewModel> Flags { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
