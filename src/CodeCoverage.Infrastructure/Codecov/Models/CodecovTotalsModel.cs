// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovTotalsModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Codecov.Models;

/// <summary>The totals of a report of the API v2 of Codecov; its <c>coverage</c> is not read, it is computed again.</summary>
/// <param name="Files">The number of files.</param>
/// <param name="Hits">The covered lines.</param>
/// <param name="Misses">The lines not covered.</param>
/// <param name="Partials">The partially covered lines.</param>
/// <param name="Branches">The number of branches.</param>
/// <param name="Methods">The number of methods.</param>
internal sealed record CodecovTotalsModel(int Files, int Hits, int Misses, int Partials, int Branches = 0, int Methods = 0);
