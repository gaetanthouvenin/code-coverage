// ------------------------------------------------------------------------------------------------
// <copyright file="FlagComparisonRowViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Comparisons;

/// <summary>A flag of a comparison.</summary>
/// <param name="Flag">The flag.</param>
/// <param name="Coverage">Its coverage at the head, or the dash.</param>
/// <param name="Delta">Its change, or <see langword="null" /> for a carried flag or without a base value.</param>
/// <param name="Trend">The sign of the change: 1, -1 or 0.</param>
/// <param name="CarriedForward">Whether its sessions were carried forward from an earlier commit.</param>
public sealed record FlagComparisonRowViewModel(string Flag, string Coverage, string? Delta, int Trend, bool CarriedForward);
