// ------------------------------------------------------------------------------------------------
// <copyright file="ParsedFile.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>A file as a coverage report names it, before its path is corrected.</summary>
/// <param name="RawPath">The path written in the report.</param>
/// <param name="Lines">The coverable lines of the file.</param>
/// <param name="Methods">
///   The methods of the file, or <see langword="null" /> when the report does not give them.
/// </param>
public sealed record ParsedFile(string RawPath, IReadOnlyList<LineCoverage> Lines, IReadOnlyList<MethodCoverage>? Methods = null);
