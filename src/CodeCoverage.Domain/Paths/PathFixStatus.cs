// ------------------------------------------------------------------------------------------------
// <copyright file="PathFixStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Paths;

/// <summary>What the <see cref="PathFixer" /> concluded about a path of a report.</summary>
public enum PathFixStatus
{
  /// <summary>The path stands for a file of the repository.</summary>
  Resolved,

  /// <summary>No file of the repository matches the path, which is dropped.</summary>
  Unresolved,

  /// <summary>Several files match equally well and nothing separates them: the path is dropped.</summary>
  Ambiguous,

  /// <summary>The path is kept, but no list of files could confirm it.</summary>
  Unverified,
}
