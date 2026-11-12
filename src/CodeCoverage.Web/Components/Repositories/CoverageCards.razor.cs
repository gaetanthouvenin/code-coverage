// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageCards.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.ViewModels.Repositories;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The cards of the project and of every flag (<see cref="Ui.CcKpiCard" />, as in the validated mockup): coverage of the
///   covered head of the default branch, change over 30 days in text and colour, and the origin of a carried forward value.
/// </summary>
public partial class CoverageCards
{
  #region Fields

  private const int MaxColumns = 4;

  #endregion

  #region Parameters

  /// <summary>Gets or sets the cards, the project first.</summary>
  [Parameter, EditorRequired]
  public IReadOnlyList<FlagCardViewModel> Cards { get; set; } = [];

  #endregion

  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion

  #region Properties

  private string GridClass => $"cc-grid cc-grid--{Math.Clamp(Cards.Count, 2, MaxColumns)}";

  #endregion

  #region Methods

  // The arrow repeats the sign in text: the colour is never the only signal.
  private static string ArrowOf(int trend) => trend switch
  {
    > 0 => "↑",
    < 0 => "↓",
    var _ => "±",
  };

  private static CcTone ToneOf(int trend) => trend switch
  {
    > 0 => CcTone.Success,
    < 0 => CcTone.Danger,
    var _ => CcTone.Neutral,
  };

  // The flags alternate the two other icon styles of the mockup; the project card is the accent.
  private static CcKpiIconStyle StyleOf(int index) => index % 2 == 1 ? CcKpiIconStyle.Ink : CcKpiIconStyle.Gradient;

  #endregion
}
