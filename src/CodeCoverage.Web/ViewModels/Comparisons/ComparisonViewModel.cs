// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Comparisons;

/// <summary>A stored comparison, formatted as the configuration of the repository says.</summary>
/// <param name="Project">The coverage of the head.</param>
/// <param name="Delta">The change of the project coverage, or <see langword="null" /> without a base report.</param>
/// <param name="Trend">The sign of the change: 1, -1 or 0.</param>
/// <param name="BaseCoverage">The coverage of the base, or <see langword="null" /> without a base report.</param>
/// <param name="Patch">The coverage of the patch, or the dash without a coverable changed line.</param>
/// <param name="PatchRatio">The coverage of the patch in percent, for the gauge, or <see langword="null" />.</param>
/// <param name="PatchHits">The changed lines hit.</param>
/// <param name="PatchLines">The coverable changed lines.</param>
/// <param name="PatchMissed">The changed lines missed or partially hit.</param>
/// <param name="PatchMissedFiles">The files with a changed line missed or partially hit.</param>
/// <param name="WorstFile">The file name with the most changed lines missed, or <see langword="null" />.</param>
/// <param name="WorstFileMissed">The changed lines missed in <paramref name="WorstFile" />.</param>
/// <param name="IndirectChanges">The number of files whose coverage moved outside the diff.</param>
/// <param name="Base">How the base was chosen.</param>
/// <param name="Diff">The lines of the <c>diff</c> table.</param>
/// <param name="IsStale">Whether a report changed since the comparison was computed.</param>
public sealed record ComparisonViewModel(
  string Project,
  string? Delta,
  int Trend,
  string? BaseCoverage,
  string Patch,
  double? PatchRatio,
  int PatchHits,
  int PatchLines,
  int PatchMissed,
  int PatchMissedFiles,
  string? WorstFile,
  int WorstFileMissed,
  int IndirectChanges,
  BaseExplanationViewModel Base,
  IReadOnlyList<DiffLineViewModel> Diff,
  bool IsStale)
{
  #region Properties

  /// <summary>Gets a value indicating whether the comparison has no base report: Δ is not computed.</summary>
  public bool BaseMissing => Base.ComparedTo is null;

  #endregion
}
