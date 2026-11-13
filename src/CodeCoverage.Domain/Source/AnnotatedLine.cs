// ------------------------------------------------------------------------------------------------
// <copyright file="AnnotatedLine.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Domain.Source;

/// <summary>A line of a source file with the coverage the report gives it.</summary>
/// <param name="Number">The number of the line, from 1.</param>
/// <param name="Text">The text of the line, without its end of line.</param>
/// <param name="State">Hit, miss or partial, or <see langword="null" /> for a line the report does not measure.</param>
/// <param name="Hits">The number of hits, as <see cref="LineCoverage.Hits" />, or <see langword="null" /> for a line not measured.</param>
/// <param name="Covered">The branches covered of a branch line, 0 otherwise.</param>
/// <param name="Total">The branches of a branch line, 0 otherwise.</param>
public sealed record AnnotatedLine(int Number, string Text, LineState? State, long? Hits, int Covered, int Total);
