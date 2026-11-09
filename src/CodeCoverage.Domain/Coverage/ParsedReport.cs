// ------------------------------------------------------------------------------------------------
// <copyright file="ParsedReport.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>What reading a coverage report gives, before its paths are corrected.</summary>
/// <param name="Format">The format of the report.</param>
/// <param name="SourceRoots">
///   The source directories the report declares (Cobertura) or implies by its name (JaCoCo), empty
///   when it has none.
/// </param>
/// <param name="Files">The files of the report.</param>
public sealed record ParsedReport(
  ReportFormat Format,
  IReadOnlyList<string> SourceRoots,
  IReadOnlyList<ParsedFile> Files);
