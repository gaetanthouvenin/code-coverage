// ------------------------------------------------------------------------------------------------
// <copyright file="FlagTotalsModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>The totals of one flag of a commit.</summary>
/// <param name="Flag">The flag, or the empty text for the whole commit.</param>
/// <param name="Totals">Its counters.</param>
/// <param name="IsCarriedForward">Whether the totals were carried forward from an ancestor.</param>
/// <param name="CarriedForwardFromSha">The commit they were carried forward from, or <see langword="null" />.</param>
public sealed record FlagTotalsModel(string Flag, CoverageTotalsModel Totals, bool IsCarriedForward, string? CarriedForwardFromSha);
