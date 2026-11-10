// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryFlagRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Notifications;

/// <summary>A row of the <c>RepositoryFlags</c> table: a flag a repository received coverage for.</summary>
internal sealed class RepositoryFlagRecord
{
  #region Properties

  /// <summary>Gets or sets the identifier of the repository.</summary>
  public long RepositoryId { get; set; }

  /// <summary>Gets or sets the flag; case-sensitive.</summary>
  public string Flag { get; set; } = string.Empty;

  /// <summary>Gets or sets when the flag was first seen, in UTC.</summary>
  public DateTimeOffset CreatedAt { get; set; }

  #endregion
}
