// ------------------------------------------------------------------------------------------------
// <copyright file="BaseExplanation.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Components.Comparisons;

/// <summary>
///   How the base of a comparison was chosen: the head, the fork point, the commit compared to and how far behind the
///   base branch was. An unknown or zero distance shows no row, as the comment says nothing of it.
/// </summary>
public partial class BaseExplanation
{
  #region Parameters

  /// <summary>Gets or sets the explanation.</summary>
  [Parameter, EditorRequired]
  public BaseExplanationViewModel Base { get; set; } = null!;

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
