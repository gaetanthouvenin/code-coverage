// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageFormatViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>How a repository writes a coverage: the precision and the rounding of its effective configuration.</summary>
/// <param name="Precision">The number of decimals (<c>coverage.precision</c>).</param>
/// <param name="Rounding">How a value is rounded (<c>coverage.round</c>): <c>Down</c>, <c>Up</c> or <c>Nearest</c>.</param>
public sealed record CoverageFormatViewModel(int Precision, string Rounding);
