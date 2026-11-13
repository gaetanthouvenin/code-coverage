// ------------------------------------------------------------------------------------------------
// <copyright file="BaseExplanationViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Comparisons;

/// <summary>How the base of a comparison was chosen, as the comment of the pull request explains it.</summary>
/// <param name="Head">The short SHA of the head.</param>
/// <param name="ForkPoint">The short SHA of the fork point, or <see langword="null" /> for a push build.</param>
/// <param name="BaseBranch">The branch the head is compared to, or <see langword="null" /> for a push build.</param>
/// <param name="ComparedTo">The short SHA of the commit whose report is the base, or <see langword="null" /> without a base report.</param>
/// <param name="ExpectedBase">The short SHA a base report is expected for, when there is none.</param>
/// <param name="BehindBy">How many commits the base branch had beyond the fork point, shown only when positive.</param>
/// <param name="Hops">How many commits before the fork point the base report was found, shown only when positive.</param>
public sealed record BaseExplanationViewModel(
  string Head,
  string? ForkPoint,
  string? BaseBranch,
  string? ComparedTo,
  string? ExpectedBase,
  int? BehindBy,
  int? Hops);
