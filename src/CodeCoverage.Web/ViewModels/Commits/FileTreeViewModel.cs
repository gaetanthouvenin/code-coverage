// ------------------------------------------------------------------------------------------------
// <copyright file="FileTreeViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>The files of a commit, as a tree and as the flat list a search goes through.</summary>
/// <param name="Roots">The nodes of the first level, in path order.</param>
/// <param name="Files">Every file, in path order.</param>
public sealed record FileTreeViewModel(IReadOnlyList<FileNodeViewModel> Roots, IReadOnlyList<FileNodeViewModel> Files)
{
  #region Properties

  /// <summary>Gets the tree of a commit without file: one not finalized yet.</summary>
  public static FileTreeViewModel Empty { get; } = new([], []);

  #endregion
}
