// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTotalsModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Uploads;

/// <summary>The counters of an upload.</summary>
/// <param name="Lines">The number of coverable lines.</param>
/// <param name="Hits">The number of covered lines.</param>
/// <param name="Misses">The number of lines that are not covered.</param>
/// <param name="Partials">The number of partially covered lines.</param>
/// <param name="Coverage">The coverage in percent, or <see langword="null" /> without a coverable line.</param>
public sealed record UploadTotalsModel(int Lines, int Hits, int Misses, int Partials, decimal? Coverage);
