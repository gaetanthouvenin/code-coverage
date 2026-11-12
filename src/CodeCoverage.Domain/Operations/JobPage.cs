// ------------------------------------------------------------------------------------------------
// <copyright file="JobPage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Operations;

/// <summary>One page of the background jobs, the most recent first.</summary>
/// <param name="Items">The jobs of the page.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of jobs a page holds at most.</param>
/// <param name="TotalCount">The number of jobs of every page.</param>
/// <remarks>A read model.</remarks>
public sealed record JobPage(IReadOnlyList<JobSummary> Items, int Page, int PageSize, int TotalCount);
