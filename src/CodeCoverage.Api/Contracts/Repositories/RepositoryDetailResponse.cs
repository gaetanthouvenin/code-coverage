// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryDetailResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Api.Contracts.Commits;

namespace CodeCoverage.Api.Contracts.Repositories;

/// <summary>A tracked repository, with its branches, its flags and the last totals of its default branch.</summary>
/// <param name="Repository">The repository.</param>
/// <param name="Branches">Its branches, by name.</param>
/// <param name="Flags">The flags its uploads ever carried.</param>
/// <param name="Totals">The totals of each flag of the covered head of the default branch; empty while none is covered.</param>
public sealed record RepositoryDetailResponse(
  RepositoryResponse Repository,
  IReadOnlyList<BranchResponse> Branches,
  IReadOnlyList<string> Flags,
  IReadOnlyList<FlagTotalsModel> Totals);
