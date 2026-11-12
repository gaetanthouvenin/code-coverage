// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryOverview.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Domain.Repositories;

/// <summary>A repository with its branches, its flags and the last totals of its default branch.</summary>
/// <param name="Repository">The repository.</param>
/// <param name="Branches">Its branches, by name.</param>
/// <param name="Flags">The flags its uploads ever carried, by name.</param>
/// <param name="Totals">
///   The totals of each flag of the covered head of the default branch, the one without flag (<c>''</c>) included, or
///   empty while that branch has no covered commit.
/// </param>
/// <remarks>A read model.</remarks>
public sealed record RepositoryOverview(
  TrackedRepository Repository,
  IReadOnlyList<Branch> Branches,
  IReadOnlyList<string> Flags,
  IReadOnlyList<CommitFlagTotals> Totals);
