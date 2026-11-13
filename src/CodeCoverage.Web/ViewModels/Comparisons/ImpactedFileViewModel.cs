// ------------------------------------------------------------------------------------------------
// <copyright file="ImpactedFileViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Comparisons;

/// <summary>A file the diff touches, as the comment of the pull request lists it.</summary>
/// <param name="Path">The path of the file.</param>
/// <param name="Href">The address of its annotated source at the head, or <see langword="null" /> for a removed file.</param>
/// <param name="IsNew">Whether the base report did not hold it.</param>
/// <param name="IsRemoved">Whether the diff removed it.</param>
/// <param name="Coverage">Its coverage at the head, or the dash when removed.</param>
/// <param name="Delta">Its change against the base, or <see langword="null" /> without a base value.</param>
/// <param name="Trend">The sign of the change: 1, -1 or 0.</param>
/// <param name="Patch">The coverage of its changed lines, or <see langword="null" /> without a coverable changed line.</param>
/// <param name="Missed">Its changed lines missed or partially hit.</param>
public sealed record ImpactedFileViewModel(
  string Path,
  string? Href,
  bool IsNew,
  bool IsRemoved,
  string Coverage,
  string? Delta,
  int Trend,
  string? Patch,
  int Missed);
