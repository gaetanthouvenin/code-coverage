// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestDetail.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.PullRequests;

/// <summary>A pull request and the comparison stored for its head, as its page shows them.</summary>
/// <param name="PullRequest">The pull request.</param>
/// <param name="Comparison">
///   The comparison stored for its head, or <see langword="null" /> while none is: the page then says the comparison is
///   pending, never that the pull request is missing.
/// </param>
/// <param name="IndirectHeadTotals">
///   The totals of the head report for the files of the indirect changes, read at read time from its file index (the
///   stored comparison keeps only their change); <see langword="null" /> without comparison.
/// </param>
/// <remarks>A read model: nothing is computed when it is read.</remarks>
public sealed record PullRequestDetail(
  PullRequest PullRequest,
  ComparisonView? Comparison,
  IReadOnlyDictionary<string, CoverageTotals>? IndirectHeadTotals = null);
