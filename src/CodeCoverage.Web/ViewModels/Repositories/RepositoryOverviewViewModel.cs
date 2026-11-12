// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryOverviewViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>The overview of a repository: its default branch and the cards of the project and of each flag.</summary>
/// <param name="DefaultBranch">The default branch.</param>
/// <param name="Cards">The project first, then every flag in ordinal order; empty when no report covers the default branch.</param>
public sealed record RepositoryOverviewViewModel(string DefaultBranch, IReadOnlyList<FlagCardViewModel> Cards)
{
  #region Properties

  /// <summary>Gets a value indicating whether a report covers the default branch.</summary>
  public bool HasCoverage => Cards.Count > 0;

  #endregion
}
