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

  /// <summary>The report of the commit has no such file. Maps to HTTP 404 Not Found.</summary>
  public const string FileNotFound = "Commit.FileNotFound";

  /// <summary>A page or a page size is out of its bounds. Maps to HTTP 400 Bad Request.</summary>
  public const string InvalidPage = "Commit.InvalidPage";

  /// <summary>The commit has no upload: there is nothing to process again.</summary>
  public const string NoUpload = "Commit.NoUpload";

  /// <summary>The raw files of an upload of the commit are no longer kept: the commit cannot be processed again.</summary>
  public const string RawExpired = "Commit.RawExpired";

  /// <summary>Another finalization saved the commit in the meantime.</summary>
  public const string ConcurrentSave = "Commit.ConcurrentSave";

  #endregion
}
