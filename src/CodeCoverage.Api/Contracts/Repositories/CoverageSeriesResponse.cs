// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageSeriesResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Repositories;

/// <summary>The coverage history of one flag of a branch.</summary>
/// <param name="Flag">The flag, case-sensitive, or <see langword="null" /> for the whole project.</param>
/// <param name="Points">The points, the oldest first.</param>
public sealed record CoverageSeriesResponse(string? Flag, IReadOnlyList<CoveragePointModel> Points);
