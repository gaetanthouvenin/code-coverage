// ------------------------------------------------------------------------------------------------
// <copyright file="DiffSource.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Diffs;

/// <summary>The GitHub API a diff comes from.</summary>
public enum DiffSource
{
  /// <summary>The list of the files of a pull request.</summary>
  PullFiles,

  /// <summary>The comparison of two commits.</summary>
  Compare,
}
