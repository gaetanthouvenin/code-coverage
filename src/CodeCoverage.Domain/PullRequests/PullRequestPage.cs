// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestPage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.PullRequests;

/// <summary>One page of the pull requests of a repository, the most recently updated first.</summary>
/// <param name="Items">The pull requests of the page.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of pull requests a page holds at most.</param>
/// <param name="TotalCount">The number of pull requests of every page.</param>
/// <remarks>A read model.</remarks>
public sealed record PullRequestPage(IReadOnlyList<PullRequest> Items, int Page, int PageSize, int TotalCount);
