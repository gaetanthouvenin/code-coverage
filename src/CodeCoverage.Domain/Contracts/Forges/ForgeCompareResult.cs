// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeCompareResult.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a forge reports when it compares two commits.</summary>
/// <param name="MergeBaseSha">The SHA of their merge base.</param>
/// <param name="AheadBy">The number of commits the head has that the base lacks.</param>
/// <param name="BehindBy">The number of commits the base has that the head lacks.</param>
/// <param name="Files">The changed files that the forge returned.</param>
/// <param name="Truncated">A value indicating whether the forge left out some of the changed files.</param>
public sealed record ForgeCompareResult(
  GitSha MergeBaseSha,
  int AheadBy,
  int BehindBy,
  IReadOnlyList<ForgeChangedFile> Files,
  bool Truncated);
