// ------------------------------------------------------------------------------------------------
// <copyright file="UploadProcessingResult.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;

namespace CodeCoverage.Application.Services;

/// <summary>What the processing of an upload gave.</summary>
/// <param name="Report">Its coverage, or <see langword="null" /> when it failed.</param>
/// <param name="ReportFiles">The number of reports it held.</param>
/// <param name="ResolvedFiles">The number of files whose path was resolved.</param>
/// <param name="UnresolvedFiles">The number of files whose path could not be resolved.</param>
/// <param name="Log">What the processing reported.</param>
/// <param name="ErrorCode">The code of its failure, or <see langword="null" />.</param>
/// <param name="Error">The description of its failure, or <see langword="null" />.</param>
internal sealed record UploadProcessingResult(
  CoverageReport? Report,
  int ReportFiles,
  int ResolvedFiles,
  int UnresolvedFiles,
  ProcessingLog Log,
  string? ErrorCode,
  string? Error);
