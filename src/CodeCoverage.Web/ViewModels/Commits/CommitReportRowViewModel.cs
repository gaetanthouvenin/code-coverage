// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReportRowViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>A file of the detailed report of a commit, or the total of its files.</summary>
/// <param name="Path">The path of the file; empty for the total.</param>
/// <param name="Hits">The number of lines hit.</param>
/// <param name="Partials">The number of lines partially hit.</param>
/// <param name="Misses">The number of lines missed.</param>
/// <param name="CodecovRatio">
///   The coverage of Codecov, <c>hits / (hits + misses + partials)</c> in percent, or <see langword="null" /> without
///   line.
/// </param>
/// <param name="Codecov">The coverage of Codecov, written as the repository writes it.</param>
/// <param name="Lines">The executed lines, or <see langword="null" /> when the index of the commit has no detail.</param>
/// <param name="Branches">The branches taken, or <see langword="null" /> when the index of the commit has no detail.</param>
/// <param name="Methods">
///   The executed methods, or <see langword="null" /> when the index has no detail or the report gives no method.
/// </param>
public sealed record CommitReportRowViewModel(
  string Path,
  int Hits,
  int Partials,
  int Misses,
  decimal? CodecovRatio,
  string Codecov,
  CoverageCountViewModel? Lines,
  CoverageCountViewModel? Branches,
  CoverageCountViewModel? Methods);
