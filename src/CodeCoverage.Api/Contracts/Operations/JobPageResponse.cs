// ------------------------------------------------------------------------------------------------
// <copyright file="JobPageResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Operations;

/// <summary>One page of the background jobs, the most recent first.</summary>
/// <param name="Items">The jobs of the page.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of jobs a page holds at most.</param>
/// <param name="TotalCount">The number of jobs of every page.</param>
public sealed record JobPageResponse(IReadOnlyList<JobResponse> Items, int Page, int PageSize, int TotalCount);
