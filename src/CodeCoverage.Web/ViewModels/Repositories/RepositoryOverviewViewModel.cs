// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryOverviewViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>The overview of a repository: its default branch, the cards of the project and of each flag, its branches and flags.</summary>
/// <param name="DefaultBranch">The default branch.</param>
/// <param name="Cards">The project first, then every flag in ordinal order; empty when no report covers the default branch.</param>
/// <param name="Branches">The branches of the repository, for the chart of history.</param>
/// <param name="Flags">The flags of the repository in ordinal order, without the project.</param>
/// <param name="Format">How the repository writes a coverage, for the values of the chart.</param>
public sealed record RepositoryOverviewViewModel(
  string DefaultBranch,
  IReadOnlyList<FlagCardViewModel> Cards,
  IReadOnlyList<string> Branches,
  IReadOnlyList<string> Flags,
  CoverageFormatViewModel Format)
{
  #region Properties

  /// <summary>Gets a value indicating whether a report covers the default branch.</summary>
  public bool HasCoverage => Cards.Count > 0;

  #endregion
}
