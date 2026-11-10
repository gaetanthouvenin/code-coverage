// ------------------------------------------------------------------------------------------------
// <copyright file="StatusSubject.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Notifications;

/// <summary>What one status rule is evaluated on: the two reports seen through the flags and the paths of the rule.</summary>
/// <param name="HeadSha">The head commit.</param>
/// <param name="Head">The view of the head report for the rule, or <see langword="null" /> when the commit has no report.</param>
/// <param name="Base">The view of the base report for the rule, or <see langword="null" /> without a base.</param>
/// <param name="ComparedToSha">The commit of the base report, or <see langword="null" /> without a base.</param>
/// <param name="ForkPointSha">The fork point of the pull request, or <see langword="null" /> for a push build.</param>
/// <param name="Patch">The coverage of the lines the patch diff adds, read in <paramref name="Head" />.</param>
/// <param name="ComparisonDiff">The diff from the base report to the head.</param>
/// <param name="IndirectChanges">The changes of coverage outside the comparison diff, between the two views.</param>
public sealed record StatusSubject(
  GitSha HeadSha,
  ReportView? Head,
  ReportView? Base,
  GitSha? ComparedToSha,
  GitSha? ForkPointSha,
  PatchCoverage Patch,
  DiffSet ComparisonDiff,
  IReadOnlyList<IndirectChange> IndirectChanges);