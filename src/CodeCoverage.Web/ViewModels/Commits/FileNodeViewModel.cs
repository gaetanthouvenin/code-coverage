// ------------------------------------------------------------------------------------------------
// <copyright file="FileNodeViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>A folder or a file of the tree of a commit, with its totals.</summary>
/// <param name="Path">The full path, the key of the node: a folder's path has no trailing slash.</param>
/// <param name="Name">The name shown: a file's name, or the folders of a single-child chain joined by <c>/</c>.</param>
/// <param name="IsFolder">A value indicating whether the node is a folder.</param>
/// <param name="Lines">The number of lines, formatted.</param>
/// <param name="Hits">The number of lines hit.</param>
/// <param name="Partials">The number of lines partially hit.</param>
/// <param name="Misses">The number of lines missed.</param>
/// <param name="Ratio">The coverage, <c>hits / (hits + misses + partials)</c> in percent, or <see langword="null" /> without line.</param>
/// <param name="Coverage">The coverage, formatted as the repository writes it.</param>
/// <param name="Children">The children of a folder in path order; none for a file.</param>
public sealed record FileNodeViewModel(
  string Path,
  string Name,
  bool IsFolder,
  string Lines,
  int Hits,
  int Partials,
  int Misses,
  decimal? Ratio,
  string Coverage,
  IReadOnlyList<FileNodeViewModel> Children);
