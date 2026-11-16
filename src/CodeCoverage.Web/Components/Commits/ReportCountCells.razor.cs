// ------------------------------------------------------------------------------------------------
// <copyright file="ReportCountCells.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Commits;

/// <summary>
///   The two cells of a group of the detailed report: « covered / total », then the percentage in a bar; a dash for a
///   count the index does not know, and a dash instead of the bar when there is nothing to cover.
/// </summary>
public partial class ReportCountCells
{
  #region Parameters

  /// <summary>Gets or sets the count, or <see langword="null" /> when it is not known.</summary>
  [Parameter]
  public CoverageCountViewModel? Count { get; set; }

  #endregion

  #region Properties

  private static string Dash => DisplayFormatter.Dash;

  #endregion
}
