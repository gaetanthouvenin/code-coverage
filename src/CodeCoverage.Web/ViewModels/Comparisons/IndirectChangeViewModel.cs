// ------------------------------------------------------------------------------------------------
// <copyright file="IndirectChangeViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Comparisons;

/// <summary>A file whose coverage moved outside the diff.</summary>
/// <param name="Path">The path of the file.</param>
/// <param name="Href">The address of its annotated source at the head, or <see langword="null" /> when the head lost it.</param>
/// <param name="Coverage">Its coverage at the head, read from the head report, or the dash.</param>
/// <param name="Delta">Its change, or <see langword="null" />.</param>
/// <param name="Trend">The sign of the change: 1, -1 or 0.</param>
public sealed record IndirectChangeViewModel(string Path, string? Href, string Coverage, string? Delta, int Trend);
