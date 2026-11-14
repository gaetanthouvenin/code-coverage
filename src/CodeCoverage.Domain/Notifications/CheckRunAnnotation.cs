// ------------------------------------------------------------------------------------------------
// <copyright file="CheckRunAnnotation.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Notifications;

/// <summary>A note of a check run on consecutive added lines of the patch that the tests do not fully cover.</summary>
/// <param name="Path">The path of the file in the repository.</param>
/// <param name="StartLine">The first line, from 1.</param>
/// <param name="EndLine">The last line, equal to <paramref name="StartLine" /> for one line.</param>
/// <param name="Message">The text of the note.</param>
public sealed record CheckRunAnnotation(string Path, int StartLine, int EndLine, string Message);
