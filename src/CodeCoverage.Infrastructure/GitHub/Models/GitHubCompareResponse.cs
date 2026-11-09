// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCompareResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The answer of <c>GET …/compare/{base}...{head}</c>.</summary>
/// <param name="MergeBaseCommit">The merge base of the two commits.</param>
/// <param name="AheadBy">The number of commits the head has that the base lacks.</param>
/// <param name="BehindBy">The number of commits the base has that the head lacks.</param>
/// <param name="Files">The changed files, or <see langword="null" /> when GitHub leaves the list out.</param>
internal sealed record GitHubCompareResponse(
  GitHubCommitRef MergeBaseCommit,
  int AheadBy,
  int BehindBy,
  IReadOnlyList<GitHubCompareFile>? Files = null);
