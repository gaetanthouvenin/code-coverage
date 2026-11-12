// ------------------------------------------------------------------------------------------------
// <copyright file="CommitPage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Commits;

/// <summary>One page of the commits of a repository, the most recently completed first.</summary>
/// <param name="Items">The commits of the page.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of commits a page holds at most.</param>
/// <param name="TotalCount">The number of commits of every page.</param>
/// <remarks>A read model. The commits not completed yet come after the completed ones.</remarks>
public sealed record CommitPage(IReadOnlyList<Commit> Items, int Page, int PageSize, int TotalCount);
