// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Api.Contracts.Commits;

namespace CodeCoverage.Api.Contracts.Comparisons;

/// <summary>A comparison stored for a head commit: project, patch, files, flags and indirect changes.</summary>
/// <param name="PullNumber">The pull request it was computed for, or <see langword="null" /> for a push build.</param>
/// <param name="Bases">The commits it was computed between.</param>
/// <param name="Head">The counters of the head.</param>
/// <param name="Base">The counters of the commit compared to, or <see langword="null" /> without one.</param>
/// <param name="Delta">The change of the project coverage in points, or <see langword="null" /> without a base.</param>
/// <param name="Patch">The coverage of the changed lines, per file.</param>
/// <param name="Files">The files the change touches or whose coverage it changes.</param>
/// <param name="Flags">The comparison of each flag.</param>
/// <param name="IndirectChanges">The files whose coverage changed without being touched.</param>
/// <param name="DiffSource">Where the diff came from: the files of the pull request or a comparison of commits.</param>
/// <param name="DiffIncomplete">
///   Whether the forge truncated or withheld a diff: the patch and the impacted files may then be incomplete.
/// </param>
/// <param name="IsStale">
///   Whether a report changed since the comparison was computed: the next notification computes it again.
/// </param>
/// <param name="ComputedAt">When it was computed, in UTC.</param>
public sealed record ComparisonResponse(
  int? PullNumber,
  BaseResolutionModel Bases,
  CoverageTotalsModel Head,
  CoverageTotalsModel? Base,
  decimal? Delta,
  PatchModel Patch,
  IReadOnlyList<ImpactedFileModel> Files,
  IReadOnlyList<FlagComparisonModel> Flags,
  IReadOnlyList<IndirectChangeModel> IndirectChanges,
  string DiffSource,
  bool DiffIncomplete,
  bool IsStale,
  DateTimeOffset ComputedAt);
