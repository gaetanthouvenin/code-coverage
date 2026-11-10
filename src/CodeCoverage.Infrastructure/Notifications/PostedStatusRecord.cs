// ------------------------------------------------------------------------------------------------
// <copyright file="PostedStatusRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Notifications;

namespace CodeCoverage.Infrastructure.Notifications;

/// <summary>A row of the <c>PostedStatuses</c> table: the last status published for a commit and a context.</summary>
internal sealed class PostedStatusRecord
{
  #region Properties

  /// <summary>Gets or sets the identifier of the commit.</summary>
  public long CommitId { get; set; }

  /// <summary>Gets or sets the context of the status; case-sensitive.</summary>
  public string Context { get; set; } = string.Empty;

  /// <summary>Gets or sets the state of the status.</summary>
  public StatusState State { get; set; }

  /// <summary>Gets or sets the description of the status.</summary>
  public string Description { get; set; } = string.Empty;

  /// <summary>Gets or sets the address the status links to, or <see langword="null" />.</summary>
  public string? TargetUrl { get; set; }

  /// <summary>Gets or sets when the status was published, in UTC.</summary>
  public DateTimeOffset PostedAt { get; set; }

  #endregion
}