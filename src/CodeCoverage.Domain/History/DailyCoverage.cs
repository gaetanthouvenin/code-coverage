// ------------------------------------------------------------------------------------------------
// <copyright file="DailyCoverage.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.History;

/// <summary>The last coverage of a day.</summary>
/// <param name="Day">The first instant of the day, in UTC.</param>
/// <param name="Coverage">The coverage in percent of the last commit finalized that day, or <see langword="null" /> when no line was measured.</param>
public sealed record DailyCoverage(DateTimeOffset Day, decimal? Coverage);
