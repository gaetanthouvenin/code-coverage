// ------------------------------------------------------------------------------------------------
// <copyright file="FlagComparisonModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Api.Contracts.Commits;

namespace CodeCoverage.Api.Contracts.Comparisons;

/// <summary>The comparison of one flag.</summary>
/// <param name="Flag">The name of the flag, case-sensitive.</param>
/// <param name="Base">Its counters in the base, or <see langword="null" />.</param>
/// <param name="Head">Its counters in the head, or <see langword="null" />.</param>
/// <param name="Patch">The counters of the changed lines its sessions cover.</param>
/// <param name="Delta">The change of its coverage in points, or <see langword="null" /> without both sides.</param>
/// <param name="CarriedForward">Whether its head report was carried forward from an earlier commit.</param>
public sealed record FlagComparisonModel(
  string Flag,
  CoverageTotalsModel? Base,
  CoverageTotalsModel? Head,
  CoverageTotalsModel Patch,
  decimal? Delta,
  bool CarriedForward);
