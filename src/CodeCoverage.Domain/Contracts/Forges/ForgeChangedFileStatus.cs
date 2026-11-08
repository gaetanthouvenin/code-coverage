// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeChangedFileStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>How a file changed between two commits.</summary>
public enum ForgeChangedFileStatus
{
  /// <summary>The file was added.</summary>
  Added,

  /// <summary>The content or the mode of the file changed.</summary>
  Modified,

  /// <summary>The file was removed.</summary>
  Removed,

  /// <summary>The file was renamed.</summary>
  Renamed,

  /// <summary>The file was copied from another one.</summary>
  Copied,

  /// <summary>The file is listed without any change.</summary>
  Unchanged,
}
