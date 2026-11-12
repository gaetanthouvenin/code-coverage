// ------------------------------------------------------------------------------------------------
// <copyright file="PatchFileModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Api.Contracts.Commits;

namespace CodeCoverage.Api.Contracts.Comparisons;

/// <summary>The coverage of the lines a change adds or modifies in one file.</summary>
/// <param name="Path">The path of the file in the head.</param>
/// <param name="Totals">The counters of its changed lines.</param>
/// <param name="DiffUnavailable">Whether the forge gave no diff of the file: its changed lines are unknown.</param>
public sealed record PatchFileModel(string Path, CoverageTotalsModel Totals, bool DiffUnavailable);
