// ------------------------------------------------------------------------------------------------
// <copyright file="RequireChangesFacts.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>What a comparison says about the conditions of <c>comment.require_changes</c>.</summary>
/// <param name="HasIndirectChanges">A value indicating whether coverage changed outside the patch.</param>
/// <param name="PatchLines">The number of coverable lines of the patch; 0 without a head report or a diff.</param>
/// <param name="PatchCoverage">The coverage of the patch in percent with 5 decimals, if it has any.</param>
/// <param name="HeadCoverage">The coverage of the head in percent with 5 decimals, if it is known.</param>
/// <param name="BaseCoverage">The coverage of the base in percent with 5 decimals, if it is known.</param>
/// <param name="ProjectThreshold">The drop of project coverage that is tolerated, in percent.</param>
public sealed record RequireChangesFacts(
  bool HasIndirectChanges,
  int PatchLines,
  decimal? PatchCoverage,
  decimal? HeadCoverage,
  decimal? BaseCoverage,
  decimal ProjectThreshold);
