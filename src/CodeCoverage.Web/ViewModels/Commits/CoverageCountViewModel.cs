// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageCountViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>A covered count against its total, as the detailed report shows it: lines, branches or methods.</summary>
/// <param name="Covered">The number covered: lines executed, branches taken or methods executed.</param>
/// <param name="Total">The number that can be covered.</param>
/// <param name="Ratio">The percentage, or <see langword="null" /> when there is nothing to cover.</param>
/// <param name="Text">The percentage written as the repository writes a coverage, or a dash.</param>
public sealed record CoverageCountViewModel(int Covered, int Total, decimal? Ratio, string Text);
