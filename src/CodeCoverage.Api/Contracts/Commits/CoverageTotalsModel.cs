// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageTotalsModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>The counters of a commit, a flag or a file.</summary>
/// <param name="Lines">The number of coverable lines.</param>
/// <param name="Hits">The number of covered lines.</param>
/// <param name="Misses">The number of lines that are not covered.</param>
/// <param name="Partials">The number of partially covered lines.</param>
/// <param name="Coverage">The coverage in percent, <c>hits / (hits + misses + partials)</c>, or <see langword="null" /> without a coverable line.</param>
public sealed record CoverageTotalsModel(int Lines, int Hits, int Misses, int Partials, decimal? Coverage);
