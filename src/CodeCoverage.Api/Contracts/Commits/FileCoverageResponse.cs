// ------------------------------------------------------------------------------------------------
// <copyright file="FileCoverageResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>The coverage of one file of a commit, line by line.</summary>
/// <param name="Path">The path of the file, as the report stores it.</param>
/// <param name="Flag">The flag the lines are read for, or <see langword="null" /> for the merged report.</param>
/// <param name="Totals">Its counters.</param>
/// <param name="Lines">Its coverable lines, by number.</param>
public sealed record FileCoverageResponse(string Path, string? Flag, CoverageTotalsModel Totals, IReadOnlyList<FileLineModel> Lines);
