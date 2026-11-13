// ------------------------------------------------------------------------------------------------
// <copyright file="CompareViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Comparisons;

/// <summary>Two commits and the comparison a notification stored between them.</summary>
/// <param name="Base">The short SHA of the commit compared to.</param>
/// <param name="Head">The short SHA of the commit compared.</param>
/// <param name="Comparison">The comparison, or <see langword="null" /> when no notification computed it.</param>
public sealed record CompareViewModel(string Base, string Head, ComparisonViewModel? Comparison);
