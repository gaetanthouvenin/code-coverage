// ------------------------------------------------------------------------------------------------
// <copyright file="AnnotatedSourceViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Source;

/// <summary>A source file at a commit with the coverage of its lines, formatted as the repository says.</summary>
/// <param name="Path">The path of the file.</param>
/// <param name="ShortSha">The short SHA of the commit.</param>
/// <param name="Flags">The flags of the commit, for the selector.</param>
/// <param name="PatchPull">The pull request whose patch lines are marked, or <see langword="null" />.</param>
/// <param name="IsCompacted">Whether the commit was compacted: only its totals remain, no file is shown.</param>
/// <param name="IsSourceAvailable">Whether the source could be read; the totals remain either way.</param>
/// <param name="Coverage">The coverage of the file.</param>
/// <param name="Lines">The coverable lines, formatted.</param>
/// <param name="Hits">The lines hit, formatted.</param>
/// <param name="Partials">The lines partially hit, formatted.</param>
/// <param name="Misses">The lines missed, formatted.</param>
/// <param name="SourceLines">The lines of the source, empty when it is unavailable.</param>
public sealed record AnnotatedSourceViewModel(
  string Path,
  string ShortSha,
  IReadOnlyList<string> Flags,
  int? PatchPull,
  bool IsCompacted,
  bool IsSourceAvailable,
  string Coverage,
  string Lines,
  string Hits,
  string Partials,
  string Misses,
  IReadOnlyList<AnnotatedLineViewModel> SourceLines);
