// ------------------------------------------------------------------------------------------------
// <copyright file="CommitStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>The state of a commit as the pages show it: the three labels of the validated mockup, and a compacted one.</summary>
public enum CommitStatus
{
  /// <summary>Its uploads are still processed: known, or pending.</summary>
  Processing,

  /// <summary>Its coverage is complete.</summary>
  Complete,

  /// <summary>Complete, then compacted: only its totals are kept.</summary>
  Compacted,

  /// <summary>Its processing failed.</summary>
  Error,
}
