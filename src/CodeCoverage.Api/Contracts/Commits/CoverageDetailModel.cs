// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageDetailModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Commits;

/// <summary>The lines, branches and methods of a file, next to its coverage of Codecov (ADR-016).</summary>
/// <param name="ExecutedLines">The number of lines that ran at least once, partial lines included.</param>
/// <param name="CoverableLines">The number of coverable lines.</param>
/// <param name="CoveredBranches">The number of branches taken.</param>
/// <param name="TotalBranches">The number of branches.</param>
/// <param name="CoveredMethods">
///   The number of methods that ran at least once, or <see langword="null" /> when the report does not
///   give the methods.
/// </param>
/// <param name="TotalMethods">The number of methods, or <see langword="null" /> when the report does not give them.</param>
/// <param name="LineCoverage">The percentage of executed lines, or <see langword="null" /> without a line.</param>
/// <param name="BranchCoverage">The percentage of branches taken, or <see langword="null" /> without a branch.</param>
/// <param name="MethodCoverage">
///   The percentage of executed methods, or <see langword="null" /> without a method or when the methods
///   are not known.
/// </param>
public sealed record CoverageDetailModel(
  int ExecutedLines,
  int CoverableLines,
  int CoveredBranches,
  int TotalBranches,
  int? CoveredMethods,
  int? TotalMethods,
  decimal? LineCoverage,
  decimal? BranchCoverage,
  decimal? MethodCoverage);
