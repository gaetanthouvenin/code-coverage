// ------------------------------------------------------------------------------------------------
// <copyright file="CarriedSession.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Commits;

/// <summary>A session of an ancestor, once moved onto the lines of the commit.</summary>
/// <param name="SourceSessionId">The identifier of the session it was copied from.</param>
/// <param name="CarriedForwardFromSha">The SHA its coverage was first carried from.</param>
/// <param name="DataUploadId">
///   The upload whose blob already holds exactly this coverage, or <see langword="null" /> when the session
///   needs a blob of its own.
/// </param>
/// <param name="Name">Its name.</param>
/// <param name="Report">Its coverage, on the lines of the commit.</param>
/// <param name="DroppedFiles">The files left out because their lines could not be followed, to report in the log.</param>
public sealed record CarriedSession(
  long SourceSessionId,
  GitSha CarriedForwardFromSha,
  long? DataUploadId,
  string Name,
  CoverageReport Report,
  IReadOnlyList<string> DroppedFiles);
