// ------------------------------------------------------------------------------------------------
// <copyright file="FileLineModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>The coverage of one line of a file.</summary>
/// <param name="Line">The number of the line, from 1.</param>
/// <param name="State">Whether it is covered: hit, miss or partial.</param>
/// <param name="Hits">How many times the line was executed.</param>
/// <param name="Branches">The covered and total branches, <c>c/t</c>, or <see langword="null" /> without branch data.</param>
public sealed record FileLineModel(int Line, string State, long Hits, string? Branches);
