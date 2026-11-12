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
public sealed record BaseResolutionModel(string HeadSha, string? ForkPointSha, string? ComparedToSha, bool IsExact);
