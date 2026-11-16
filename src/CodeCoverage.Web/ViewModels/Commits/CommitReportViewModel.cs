// ------------------------------------------------------------------------------------------------
// <copyright file="CommitReportViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>The detailed report of a commit: the coverage of Codecov, then the lines, branches and methods of each file.</summary>
/// <param name="ShortSha">The short SHA of the commit.</param>
/// <param name="Branch">The branch of the commit, or <see langword="null" />.</param>
/// <param name="Flags">The flags of the commit, the only ones the filter offers.</param>
/// <param name="IsCompacted">A value indicating whether the commit was compacted: only its totals remain.</param>
/// <param name="HasDetail">
///   A value indicating whether the index of the commit has the detail of its files; <see langword="false" /> for a
///   commit processed before it was stored (ADR-016).
/// </param>
/// <param name="Target">The fixed target of the project status, in percent, or <see langword="null" /> without one.</param>
/// <param name="TargetText">The target written as the repository writes a coverage, or <see langword="null" />.</param>
/// <param name="Total">The sum of the files.</param>
/// <param name="Files">The files, in path order.</param>
public sealed record CommitReportViewModel(
  string ShortSha,
  string? Branch,
  IReadOnlyList<string> Flags,
  bool IsCompacted,
  bool HasDetail,
  decimal? Target,
  string? TargetText,
  CommitReportRowViewModel Total,
  IReadOnlyList<CommitReportRowViewModel> Files);
