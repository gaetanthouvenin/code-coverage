// ------------------------------------------------------------------------------------------------
// <copyright file="CommitFlagRowViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>A flag of a commit, as the table of its flags shows it.</summary>
/// <param name="Flag">The flag.</param>
/// <param name="Coverage">Its coverage, formatted.</param>
/// <param name="Delta">Its change against the base of the push comparison, formatted with its sign, or <see langword="null" />.</param>
/// <param name="Trend">The direction of the change: positive, negative or zero.</param>
/// <param name="CarriedForwardFrom">The short SHA its coverage was carried forward from, or <see langword="null" /> for an upload of this commit.</param>
public sealed record CommitFlagRowViewModel(string Flag, string Coverage, string? Delta, int Trend, string? CarriedForwardFrom);
