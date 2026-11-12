// ------------------------------------------------------------------------------------------------
// <copyright file="CoveragePoint.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.History;

/// <summary>One point of a coverage history.</summary>
/// <param name="At">
///   The date of the commit for a history by commit; the first instant of the day or of the week otherwise, in UTC.
/// </param>
/// <param name="Sha">The SHA of the commit the value comes from: the last one of its period.</param>
/// <param name="Lines">The number of coverable lines.</param>
/// <param name="Hits">The number of covered lines.</param>
/// <param name="Misses">The number of lines that are not covered.</param>
/// <param name="Partials">The number of partially covered lines.</param>
/// <param name="Coverage">The coverage in percent, or <see langword="null" /> without a coverable line.</param>
public sealed record CoveragePoint(DateTimeOffset At, GitSha Sha, int Lines, int Hits, int Misses, int Partials, decimal? Coverage);
