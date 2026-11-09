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

  /// <summary>Another finalization saved the commit in the meantime.</summary>
  public const string ConcurrentSave = "Commit.ConcurrentSave";

  #endregion
}
