// ------------------------------------------------------------------------------------------------
// <copyright file="CommitState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Commits;

/// <summary>Where a commit stands in the processing of its coverage.</summary>
public enum CommitState
{
  /// <summary>Seen by a webhook, with no upload yet.</summary>
  Known,

  /// <summary>At least one upload was received and the commit is not finalized.</summary>
  Pending,

  /// <summary>Finalized: its report and its totals are available.</summary>
  Complete,

  /// <summary>Its finalization failed.</summary>
  Error,

  /// <summary>Its detailed report was removed; its totals remain.</summary>
  Compacted,
}
