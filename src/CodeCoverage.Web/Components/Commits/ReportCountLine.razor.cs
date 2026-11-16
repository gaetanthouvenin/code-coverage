// ------------------------------------------------------------------------------------------------
// <copyright file="ReportCountLine.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Mapping;
using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.Components.Commits;

/// <summary>
///   A line of a card of the detailed report, on a narrow screen: its label, « covered / total », then the percentage
///   in a bar, with the dashes of <see cref="ReportCountCells" />.
/// </summary>
public partial class ReportCountLine
{
  #region Parameters

  /// <summary>Gets or sets the label of the line: lines, branches or methods.</summary>
  [Parameter, EditorRequired]
  public string Label { get; set; } = null!;

  /// <summary>Gets or sets the count, or <see langword="null" /> when it is not known.</summary>
  [Parameter]
  public CoverageCountViewModel? Count { get; set; }

  #endregion

  #region Properties

  private static string Dash => DisplayFormatter.Dash;

  #endregion
}
