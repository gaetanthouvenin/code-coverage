// ------------------------------------------------------------------------------------------------
// <copyright file="CommitPageViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>One page of the commits of a branch, with the branches to choose from.</summary>
/// <param name="Branch">The branch listed.</param>
/// <param name="Branches">The branches of the repository, the default one included.</param>
/// <param name="Rows">The commits of the page, the most recently completed first.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of commits a page holds at most.</param>
/// <param name="TotalCount">The number of commits of the branch.</param>
public sealed record CommitPageViewModel(
  string Branch,
  IReadOnlyList<string> Branches,
  IReadOnlyList<CommitRowViewModel> Rows,
  int Page,
  int PageSize,
  int TotalCount);
