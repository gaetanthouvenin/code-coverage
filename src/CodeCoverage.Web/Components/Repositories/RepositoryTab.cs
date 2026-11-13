// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryTab.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>A tab of the header of a repository, imposed by a page whose address the tab's own address does not prefix.</summary>
public enum RepositoryTab
{
  /// <summary>The overview of the repository.</summary>
  Overview,

  /// <summary>The commits, also active on a commit, its annotated source and a free comparison.</summary>
  Commits,

  /// <summary>The pull requests, also active on a pull request.</summary>
  Pulls,

  /// <summary>The settings of the repository.</summary>
  Settings,
}
