// ------------------------------------------------------------------------------------------------
// <copyright file="ImpactedFileModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Api.Contracts.Commits;

namespace CodeCoverage.Api.Contracts.Comparisons;

/// <summary>A file the change touches, or whose coverage it changes.</summary>
/// <param name="Path">The path of the file in the head.</param>
/// <param name="PreviousPath">Its path in the base when it was renamed, or <see langword="null" />.</param>
/// <param name="Status">What the change did to it: added, modified, removed or renamed.</param>
/// <param name="Base">Its counters in the base, or <see langword="null" /> when the base does not cover it.</param>
/// <param name="Head">Its counters in the head, or <see langword="null" /> when the head does not cover it.</param>
/// <param name="Patch">The counters of its changed lines.</param>
/// <param name="Delta">The change of its coverage in points, or <see langword="null" /> without both sides.</param>
public sealed record ImpactedFileModel(
  string Path,
  string? PreviousPath,
  string Status,
  CoverageTotalsModel? Base,
  CoverageTotalsModel? Head,
  CoverageTotalsModel Patch,
  decimal? Delta);
