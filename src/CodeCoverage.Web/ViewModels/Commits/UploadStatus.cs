// ------------------------------------------------------------------------------------------------
// <copyright file="UploadStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Commits;

/// <summary>The state of a row of the uploads and sessions of a commit.</summary>
public enum UploadStatus
{
  /// <summary>Received or being processed, or processed but not counted in the commit yet.</summary>
  Processing,

  /// <summary>Processed and counted in the totals of the commit.</summary>
  Processed,

  /// <summary>Failed: the reason is its error code.</summary>
  Failed,

  /// <summary>A session carried forward from an earlier commit.</summary>
  CarriedForward,
}
