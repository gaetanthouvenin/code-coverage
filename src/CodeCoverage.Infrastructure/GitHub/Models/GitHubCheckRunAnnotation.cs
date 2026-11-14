// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubCheckRunAnnotation.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>An annotation of a check run.</summary>
/// <param name="Path">The path of the file in the repository.</param>
/// <param name="StartLine">The first line.</param>
/// <param name="EndLine">The last line.</param>
/// <param name="AnnotationLevel">Always <c>warning</c>.</param>
/// <param name="Message">The text of the note.</param>
internal sealed record GitHubCheckRunAnnotation(string Path, int StartLine, int EndLine, string AnnotationLevel, string Message);
