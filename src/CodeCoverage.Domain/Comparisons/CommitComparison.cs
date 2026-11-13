// ------------------------------------------------------------------------------------------------
// <copyright file="CommitComparison.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>The comparison computed for a commit, as it is stored: what was compared, and the result.</summary>
/// <param name="CommitId">The identifier of the head commit.</param>
/// <param name="PullNumber">The number of the pull request the commit was compared in, or <see langword="null" /> for a push build.</param>
/// <param name="ForkPointSha">The fork point of the pull request, or <see langword="null" />.</param>
/// <param name="ComparedToSha">The commit whose report is the base, or <see langword="null" /> without a base.</param>
/// <param name="HeadRevision">The revision of the head report the comparison read.</param>
/// <param name="BaseRevision">The revision of the base report the comparison read, or <see langword="null" /> without a base.</param>
/// <param name="DiffSource">Where the patch diff came from.</param>
/// <param name="Result">The comparison.</param>
/// <param name="ComputedAt">When the comparison was computed, in UTC.</param>
/// <param name="BehindBy">
///   How many commits the base branch had beyond the fork point when the comparison was computed, or
///   <see langword="null" /> when unknown (a comparison stored before it was kept, or no fork point).
/// </param>
/// <param name="Hops">
///   How many commits before the commit the walk started at the base report was found: the fork point of a pull
///   request, the first parent of a push build (which has no merge base, so the count says nothing of one); <c>0</c> on
///   that commit itself, or <see langword="null" /> without a base report or when unknown.
/// </param>
/// <remarks>
///   A commit has one stored comparison per pull request, and one as a push build. The revisions tell
///   whether it still describes the reports: a report finalized again makes it stale.
/// </remarks>
public sealed record CommitComparison(
  long CommitId,
  int? PullNumber,
  GitSha? ForkPointSha,
  GitSha? ComparedToSha,
  int HeadRevision,
  int? BaseRevision,
  DiffSource DiffSource,
  Comparison Result,
  DateTimeOffset ComputedAt,
  int? BehindBy = null,
  int? Hops = null);