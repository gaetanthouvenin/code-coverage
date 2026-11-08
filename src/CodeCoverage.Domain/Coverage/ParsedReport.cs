// ------------------------------------------------------------------------------------------------
// <copyright file="ParsedReport.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Coverage;

/// <summary>What reading a coverage report gives, before its paths are corrected.</summary>
/// <param name="Format">The format of the report.</param>
/// <param name="SourceRoots">
///   The source directories the report declares (Cobertura), empty when it declares none.
/// </param>
/// <param name="Files">The files of the report.</param>
public sealed record ParsedReport(
  ReportFormat Format,
  IReadOnlyList<string> SourceRoots,
  IReadOnlyList<ParsedFile> Files);
