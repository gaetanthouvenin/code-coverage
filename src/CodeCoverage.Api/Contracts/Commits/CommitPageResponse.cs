// ------------------------------------------------------------------------------------------------
// <copyright file="CommitPageResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>One page of the commits of a repository, the most recently completed first.</summary>
/// <param name="Items">The commits of the page, without their sessions, uploads, flags and statuses.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of commits a page holds at most.</param>
/// <param name="TotalCount">The number of commits of every page.</param>
public sealed record CommitPageResponse(IReadOnlyList<CommitResponse> Items, int Page, int PageSize, int TotalCount);
