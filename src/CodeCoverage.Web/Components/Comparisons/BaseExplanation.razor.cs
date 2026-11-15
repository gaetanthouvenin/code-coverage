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

  #region Methods

  // « a1b2c3d on main », with the SHA a link: the text of the culture around the fork point, the branch kept as text.
  private (string Before, string After) AroundForkPoint(string branch)
  {
    const string Marker = "\u0000";
    var text = L["Comparison_OnBranch", Marker, branch].Value;
    var at = text.IndexOf(Marker, StringComparison.Ordinal);

    return at < 0 ? (text, string.Empty) : (text[..at], text[(at + Marker.Length)..]);
  }

  #endregion
}
