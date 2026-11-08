// ------------------------------------------------------------------------------------------------
// <copyright file="ParsedFile.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>A file as a coverage report names it, before its path is corrected.</summary>
/// <param name="RawPath">The path written in the report.</param>
/// <param name="Lines">The coverable lines of the file.</param>
public sealed record ParsedFile(string RawPath, IReadOnlyList<LineCoverage> Lines);
