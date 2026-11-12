// ------------------------------------------------------------------------------------------------
// <copyright file="FileTotalsResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>A file of the report of a commit, with its counters.</summary>
/// <param name="Path">The path of the file, as the report stores it.</param>
/// <param name="Totals">Its counters.</param>
public sealed record FileTotalsResponse(string Path, CoverageTotalsModel Totals);
