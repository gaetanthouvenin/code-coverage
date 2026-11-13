// ------------------------------------------------------------------------------------------------
// <copyright file="AnnotatedLineViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Source;

/// <summary>A line of the annotated source.</summary>
/// <param name="Number">The number of the line, from 1.</param>
/// <param name="Text">The text of the line, as read from the forge: never markup.</param>
/// <param name="State">Its coverage.</param>
/// <param name="Hits">The hits of a measured line, <c>c/t</c> for a partial one, or <see langword="null" />.</param>
/// <param name="InPatch">Whether the pull request the page comes from added or changed it.</param>
public sealed record AnnotatedLineViewModel(int Number, string Text, SourceLineState State, string? Hits, bool InPatch);
