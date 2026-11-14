// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardWaitRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Commits;

/// <summary>
///   A pending ancestor a commit passed after the wait of the carryforward: one row of the <c>CarryforwardWaits</c>
///   table (#165).
/// </summary>
/// <remarks>
///   Written by the finalization of the waiting commit, in its transaction, and replaced at each of its finalizations: when
///   the ancestor completes, it wakes the commits that wait for it, and only those.
/// </remarks>
internal sealed class CarryforwardWaitRecord
{
  #region Properties

  /// <summary>Gets or sets the identifier of the commit that waits.</summary>
  public long CommitId { get; set; }

  /// <summary>Gets or sets the identifier of the pending ancestor it passed.</summary>
  public long AncestorId { get; set; }

  #endregion
}
