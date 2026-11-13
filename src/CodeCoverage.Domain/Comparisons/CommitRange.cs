// ------------------------------------------------------------------------------------------------
// <copyright file="CommitRange.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Comparisons;

/// <summary>Two commits of a repository and the comparison a notification stored between them, as the compare page shows them.</summary>
/// <param name="Base">The commit compared to.</param>
/// <param name="Head">The commit compared.</param>
/// <param name="Comparison">
///   The comparison stored for the head against the base, or <see langword="null" /> when no notification compared them:
///   the page then says it was not computed, never that the commits are missing.
/// </param>
/// <param name="IndirectHeadTotals">
///   The totals of the head report for the files of the indirect changes, read at read time; <see langword="null" />
///   without comparison.
/// </param>
/// <remarks>A read model: nothing is computed when it is read.</remarks>
public sealed record CommitRange(
  GitSha Base,
  GitSha Head,
  ComparisonView? Comparison,
  IReadOnlyDictionary<string, CoverageTotals>? IndirectHeadTotals = null);
