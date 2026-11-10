// ------------------------------------------------------------------------------------------------
// <copyright file="BaseResolution.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>What a commit is compared with, and the diffs each part of the comparison reads (ARCHITECTURE §7.4).</summary>
/// <param name="PullRequest">The pull request of the commit, or <see langword="null" /> for a push build.</param>
/// <param name="ForkPoint">The merge base of the pull request and its base branch, or <see langword="null" /> for a push build.</param>
/// <param name="ComparedTo">The commit whose report is the base, or <see langword="null" /> when none was found.</param>
/// <param name="PatchDiff">The diff of the patch coverage: from the fork point, or from <paramref name="ComparedTo" /> for a push build.</param>
/// <param name="ComparisonDiff">The diff from <paramref name="ComparedTo" />, read by the adjusted base and the indirect changes.</param>
/// <param name="BehindBy">How many commits of the base branch the commit lacks, or <see langword="null" /> when the forge did not say.</param>
/// <param name="Hops">
///   How many commits separate <paramref name="ComparedTo" /> from the commit the walk started at, the fork
///   point or the first parent; <see langword="null" /> without a base.
/// </param>
/// <remarks>
///   Every diff comes from the API of the forge, never from the git history of a CI agent. A diff the forge
///   could not give is empty and marked truncated, so that what reads it says the comparison is incomplete.
/// </remarks>
public sealed record BaseResolution(
  PullRequest? PullRequest,
  GitSha? ForkPoint,
  Commit? ComparedTo,
  DiffSet PatchDiff,
  DiffSet ComparisonDiff,
  int? BehindBy,
  int? Hops)
{
  #region Properties

  /// <summary>Gets a value indicating whether the base is the fork point itself, or the first parent for a push build.</summary>
  public bool IsExactBase => Hops is 0;

  /// <summary>Gets a value indicating whether a diff of the comparison lacks files.</summary>
  public bool DiffIncomplete => PatchDiff.Truncated || ComparisonDiff.Truncated;

  #endregion

  #region Methods

  /// <summary>Builds the diff of a comparison that has no base: nothing changed, and nothing is missing.</summary>
  /// <returns>The empty diff.</returns>
  public static DiffSet NoDiff() => new([], Truncated: false, DiffSource.Compare);

  /// <summary>Builds the diff the forge could not give: empty, and marked truncated.</summary>
  /// <param name="source">The reading that failed.</param>
  /// <returns>The diff.</returns>
  public static DiffSet UnavailableDiff(DiffSource source) => new([], Truncated: true, source);

  #endregion
}