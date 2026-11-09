// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Commits;

/// <summary>What the finalization of a commit found for its carried-forward flags.</summary>
public enum CarryforwardStatus
{
  /// <summary>Nothing had to be carried forward.</summary>
  None,

  /// <summary>Every missing flag was carried forward from an ancestor.</summary>
  Applied,

  /// <summary>A closer ancestor was still pending, and the next complete one was used.</summary>
  AncestorPending,

  /// <summary>No ancestor holds a missing flag.</summary>
  NotFound,
}
