// ------------------------------------------------------------------------------------------------
// <copyright file="FlagCardViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>The card of the project or of one flag on the overview of a repository.</summary>
/// <param name="Flag">The flag, or an empty text for the whole project.</param>
/// <param name="Coverage">The coverage of the covered head of the default branch, formatted.</param>
/// <param name="Delta">The size of the change over 30 days, formatted without sign, or <see langword="null" /> when it is unknown.</param>
/// <param name="Trend">The direction of the change: positive, negative or zero.</param>
/// <param name="CarriedForwardFrom">The short SHA its coverage was carried forward from, or <see langword="null" />.</param>
public sealed record FlagCardViewModel(string Flag, string Coverage, string? Delta, int Trend, string? CarriedForwardFrom)
{
  #region Properties

  /// <summary>Gets a value indicating whether the card is the one of the whole project.</summary>
  public bool IsProject => Flag.Length == 0;

  #endregion
}
