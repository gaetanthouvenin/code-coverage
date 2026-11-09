// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryTreeRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Infrastructure.Commits;

/// <summary>The files of a repository at a SHA: one row of the <c>RepositoryTrees</c> table.</summary>
internal sealed class RepositoryTreeRecord
{
  #region Properties

  /// <summary>Gets or sets the identifier of the repository.</summary>
  public long RepositoryId { get; set; }

  /// <summary>Gets or sets the SHA that was read.</summary>
  public required GitSha Sha { get; set; }

  /// <summary>Gets or sets the paths and the blob SHAs, in the format of <see cref="RepositoryTreeCodec" />.</summary>
  public byte[] Data { get; set; } = [];

  /// <summary>Gets or sets a value indicating whether the forge left out some of the files.</summary>
  public bool Truncated { get; set; }

  /// <summary>Gets or sets when the forge was read, in UTC.</summary>
  public DateTimeOffset FetchedAt { get; set; }

  #endregion
}
