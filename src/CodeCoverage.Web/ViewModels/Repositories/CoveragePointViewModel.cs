// ------------------------------------------------------------------------------------------------
// <copyright file="CoveragePointViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>A point of the history of a coverage.</summary>
/// <param name="At">The first instant of its day or week, in UTC.</param>
/// <param name="Coverage">The last coverage of the period in percent, or <see langword="null" /> when no line was measured.</param>
public sealed record CoveragePointViewModel(DateTimeOffset At, decimal? Coverage);
