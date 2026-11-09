// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardSource.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Commits;

/// <summary>A session of an ancestor that may be carried forward.</summary>
/// <param name="SessionId">The identifier of the session.</param>
/// <param name="AncestorSha">The SHA of the ancestor.</param>
/// <param name="CarriedForwardFromSha">The SHA it was itself carried from, or <see langword="null" /> for an uploaded session.</param>
/// <param name="DataUploadId">The upload whose blob holds its coverage, or <see langword="null" /> when it has its own blob.</param>
/// <param name="Flags">Its flags.</param>
/// <param name="Name">Its name, or <see langword="null" />.</param>
/// <param name="Report">Its coverage, on the lines of the ancestor.</param>
public sealed record CarryforwardSource(
  long SessionId,
  GitSha AncestorSha,
  GitSha? CarriedForwardFromSha,
  long? DataUploadId,
  IReadOnlyList<string> Flags,
  string? Name,
  CoverageReport Report);
