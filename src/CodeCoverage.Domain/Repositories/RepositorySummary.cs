// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySummary.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.History;

namespace CodeCoverage.Domain.Repositories;

/// <summary>A repository as the list of the repositories shows it.</summary>
/// <param name="Repository">The repository.</param>
/// <param name="CoveredHead">
///   The covered head of its default branch, with its project totals: the last commit shown and its coverage agree; or
///   <see langword="null" /> when no report covers the default branch.
/// </param>
/// <param name="Flags">Its flags, in ordinal order.</param>
/// <param name="History">The last project coverage of each day of the last 30 days on its default branch, the oldest first.</param>
/// <remarks>A read model, read from the database only.</remarks>
public sealed record RepositorySummary(TrackedRepository Repository, Commit? CoveredHead, IReadOnlyList<string> Flags, IReadOnlyList<DailyCoverage> History);
