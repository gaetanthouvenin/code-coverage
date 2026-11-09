// ------------------------------------------------------------------------------------------------
// <copyright file="UploadState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Commits;

/// <summary>Where an upload stands in its processing.</summary>
public enum UploadState
{
  /// <summary>Stored and queued, not read yet.</summary>
  Received,

  /// <summary>Being read by a worker.</summary>
  Processing,

  /// <summary>Read: its coverage is stored.</summary>
  Processed,

  /// <summary>Its reports could not be used.</summary>
  Failed,
}
