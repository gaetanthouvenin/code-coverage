// ------------------------------------------------------------------------------------------------
// <copyright file="PatchModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Api.Contracts.Commits;

namespace CodeCoverage.Api.Contracts.Comparisons;

/// <summary>The coverage of the lines a change adds or modifies.</summary>
/// <param name="Totals">The counters of every changed line.</param>
/// <param name="Files">The counters of each file, by path.</param>
public sealed record PatchModel(CoverageTotalsModel Totals, IReadOnlyList<PatchFileModel> Files);
