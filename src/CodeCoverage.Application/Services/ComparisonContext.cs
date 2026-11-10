// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;

namespace CodeCoverage.Application.Services;

/// <summary>A comparison that was just built, with what it was built from: what the rules of a notification read.</summary>
/// <param name="Comparison">The comparison, as it was stored.</param>
/// <param name="Resolution">What the commit was compared with, and the diffs.</param>
/// <param name="HeadSessions">The sessions of the head commit, with their reports.</param>
/// <param name="BaseSessions">The sessions of the base commit, or <see langword="null" /> without a base.</param>
internal sealed record ComparisonContext(
  CommitComparison Comparison,
  BaseResolution Resolution,
  IReadOnlyList<SessionCoverage> HeadSessions,
  IReadOnlyList<SessionCoverage>? BaseSessions);