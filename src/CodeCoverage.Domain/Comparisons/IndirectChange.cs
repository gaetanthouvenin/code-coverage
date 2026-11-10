// ------------------------------------------------------------------------------------------------
// <copyright file="IndirectChange.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Comparisons;

/// <summary>A change of coverage in a file the diff does not touch (ARCHITECTURE §7.5).</summary>
/// <param name="Path">The path of the file.</param>
/// <param name="Kind">How the file changed.</param>
/// <param name="DeltaHits">How many more lines are hit in the head; negative for fewer.</param>
/// <param name="DeltaMisses">How many more lines are missed in the head; negative for fewer.</param>
/// <param name="DeltaPartials">How many more lines are partially hit in the head; negative for fewer.</param>
/// <param name="DeltaCoverage">
///   The change of coverage of the file, not rounded, or <see langword="null" /> when one report does not
///   hold the file.
/// </param>
public sealed record IndirectChange(
  string Path,
  IndirectChangeKind Kind,
  int DeltaHits,
  int DeltaMisses,
  int DeltaPartials,
  decimal? DeltaCoverage)
{
  #region Properties

  /// <summary>Gets a value indicating whether the change fails the <c>changes</c> status.</summary>
  /// <value>
  ///   <see langword="true" /> when a file both reports hold has more lines missed or partially hit, as
  ///   <c>is_a_change_worth_noting</c> of Codecov: a file that appears or disappears is listed, never failing.
  /// </value>
  public bool IsFailing => Kind is IndirectChangeKind.Changed && DeltaMisses + DeltaPartials > 0;

  #endregion
}