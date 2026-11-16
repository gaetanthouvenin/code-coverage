// ------------------------------------------------------------------------------------------------
// <copyright file="FileTotalsResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>A file of the report of a commit, with its counters.</summary>
/// <param name="Path">The path of the file, as the report stores it.</param>
/// <param name="Totals">Its counters, those of Codecov.</param>
/// <param name="Detail">
///   Its lines, branches and methods, or <see langword="null" /> for a commit processed before the detail
///   was stored.
/// </param>
public sealed record FileTotalsResponse(string Path, CoverageTotalsModel Totals, CoverageDetailModel? Detail);
