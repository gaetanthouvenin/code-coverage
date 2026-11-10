// ------------------------------------------------------------------------------------------------
// <copyright file="CommitErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Features.Commits;

/// <summary>The error codes of the commits.</summary>
public static class CommitErrors
{
  #region Fields

  /// <summary>The commit does not exist in this organization.</summary>
  public const string NotFound = "Commit.NotFound";

  /// <summary>The coverage of a processed upload of the commit no longer exists: nothing can rebuild its report.</summary>
  public const string Compacted = "Commit.Compacted";

  /// <summary>Another finalization saved the commit in the meantime.</summary>
  public const string ConcurrentSave = "Commit.ConcurrentSave";

  #endregion
}
