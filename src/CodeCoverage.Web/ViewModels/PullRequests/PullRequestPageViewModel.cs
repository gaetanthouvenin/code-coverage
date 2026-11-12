// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestPageViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.PullRequests;

/// <summary>One page of the pull requests of a repository in one state.</summary>
/// <param name="State">The state listed: <c>open</c>, <c>merged</c>, <c>closed</c> or <c>all</c>.</param>
/// <param name="Rows">The pull requests of the page, the most recently updated first.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of pull requests a page holds at most.</param>
/// <param name="TotalCount">The number of pull requests in that state.</param>
public sealed record PullRequestPageViewModel(string State, IReadOnlyList<PullRequestRowViewModel> Rows, int Page, int PageSize, int TotalCount);
