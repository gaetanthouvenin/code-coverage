// ------------------------------------------------------------------------------------------------
// <copyright file="BaseResolutionModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Comparisons;

/// <summary>The commits a comparison was computed between.</summary>
/// <param name="HeadSha">The full SHA of the head commit.</param>
/// <param name="ForkPointSha">
///   The full SHA of the commit the pull request forked from, or <see langword="null" /> for a push build or when it is
///   not known.
/// </param>
/// <param name="ComparedToSha">
///   The full SHA of the commit whose report the head was compared to, or <see langword="null" /> when no report was
///   found to compare against.
/// </param>
/// <param name="IsExact">
///   Whether the report compared to is the one of the fork point itself; otherwise it is the nearest reported ancestor.
/// </param>
/// <param name="BehindBy">
///   How many commits the base branch had beyond the fork point, or <see langword="null" /> when unknown (a comparison
///   stored before the count was kept).
/// </param>
/// <param name="Hops">
///   How many commits before the fork point the base report was found, <c>0</c> on the fork point itself, or
///   <see langword="null" /> without a base report or when unknown.
/// </param>
public sealed record BaseResolutionModel(string HeadSha, string? ForkPointSha, string? ComparedToSha, bool IsExact, int? BehindBy = null, int? Hops = null);
