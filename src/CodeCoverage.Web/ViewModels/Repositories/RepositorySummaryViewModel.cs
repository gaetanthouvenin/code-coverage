// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySummaryViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.ViewModels.Commits;

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>A repository of the home page.</summary>
/// <param name="Forge">The forge segment of its address.</param>
/// <param name="Owner">Its owner.</param>
/// <param name="Name">Its name.</param>
/// <param name="IsInactive">Whether an administrator disabled it.</param>
/// <param name="Coverage">The coverage of the covered head of its default branch, with two decimals, or a dash.</param>
/// <param name="CoverageValue">That coverage in percent, for its bar, or <see langword="null" />.</param>
/// <param name="Trend">The last coverage of each day of the last 30 days, the oldest first.</param>
/// <param name="LastSha">The short SHA of that covered head, or <see langword="null" />.</param>
/// <param name="LastStatus">The state of that commit, or <see langword="null" />.</param>
/// <param name="LastAt">When that commit was completed, or <see langword="null" />.</param>
/// <param name="Flags">Its flags, in ordinal order.</param>
public sealed record RepositorySummaryViewModel(
  string Forge,
  string Owner,
  string Name,
  bool IsInactive,
  string Coverage,
  decimal? CoverageValue,
  IReadOnlyList<decimal?> Trend,
  string? LastSha,
  CommitStatus? LastStatus,
  DateTimeOffset? LastAt,
  IReadOnlyList<string> Flags);
