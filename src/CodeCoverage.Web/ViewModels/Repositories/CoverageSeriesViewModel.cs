// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageSeriesViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>The history of the coverage of the project or of one flag on a branch.</summary>
/// <param name="Flag">The flag, or an empty text for the whole project.</param>
/// <param name="Points">One point per day or per week, the oldest first.</param>
public sealed record CoverageSeriesViewModel(string Flag, IReadOnlyList<CoveragePointViewModel> Points);
