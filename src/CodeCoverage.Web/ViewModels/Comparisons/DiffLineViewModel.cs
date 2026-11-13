// ------------------------------------------------------------------------------------------------
// <copyright file="DiffLineViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Comparisons;

/// <summary>A line of the <c>diff</c> table, laid out as the comment of the pull request lays it out.</summary>
/// <param name="Text">The text of the line, its columns padded.</param>
/// <param name="Kind">How the line is drawn.</param>
public sealed record DiffLineViewModel(string Text, DiffLineKind Kind);
