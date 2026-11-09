// ------------------------------------------------------------------------------------------------
// <copyright file="DiffCacheRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Infrastructure.Commits;

/// <summary>A cached diff: one row of the <c>DiffCache</c> table.</summary>
internal sealed class DiffCacheRecord
{
  #region Properties

  /// <summary>Gets or sets the identifier of the repository.</summary>
  public long RepositoryId { get; set; }

  /// <summary>Gets or sets the SHA of the base.</summary>
  public GitSha BaseSha { get; set; } = null!;

  /// <summary>Gets or sets the SHA of the head.</summary>
  public GitSha HeadSha { get; set; } = null!;

  /// <summary>Gets or sets the API the diff comes from.</summary>
  public DiffSource Kind { get; set; }

  /// <summary>Gets or sets the files, as gzip JSON.</summary>
  public byte[] Data { get; set; } = [];

  /// <summary>Gets or sets a value indicating whether the forge left out some files.</summary>
  public bool Incomplete { get; set; }

  /// <summary>Gets or sets when the forge was read, in UTC.</summary>
  public DateTimeOffset FetchedAt { get; set; }

  #endregion
}
