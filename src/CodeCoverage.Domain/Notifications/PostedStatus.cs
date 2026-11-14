// ------------------------------------------------------------------------------------------------
// <copyright file="PostedStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Notifications;

/// <summary>A commit status as it was last published on the forge.</summary>
/// <remarks>
///   Kept so that the same status is not published twice, and so that the pages and the API show the
///   statuses of a commit without asking the forge.
/// </remarks>
public sealed record PostedStatus
{
  #region Fields

  /// <summary>The maximum length of a context: with the commit, it is the key of the row on both engines.</summary>
  public const int ContextMaxLength = 446;

  /// <summary>The maximum length of a target address.</summary>
  public const int TargetUrlMaxLength = 2048;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="PostedStatus" /> class.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="status">The status that was published.</param>
  /// <param name="targetUrl">The address the status links to, or <see langword="null" /> when the instance has no public address.</param>
  /// <param name="postedAt">When the status was published; converted to UTC.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="commitId" /> is not positive.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="status" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   The context is longer than <see cref="ContextMaxLength" />, or <paramref name="targetUrl" /> is empty or
  ///   longer than <see cref="TargetUrlMaxLength" />; <see langword="null" /> is the way to give none.
  /// </exception>
  public PostedStatus(long commitId, CommitStatus status, string? targetUrl, DateTimeOffset postedAt)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(commitId);
    ArgumentNullException.ThrowIfNull(status);
    ArgumentException.ThrowIf(targetUrl is not null && string.IsNullOrWhiteSpace(targetUrl), "An address is not empty: pass null to give none.", nameof(targetUrl));
    ArgumentException.ThrowIf(status.Context.Length > ContextMaxLength, $"A context cannot be longer than {ContextMaxLength} characters.", nameof(status));
    ArgumentException.ThrowIf(targetUrl?.Length > TargetUrlMaxLength, $"The value cannot be longer than {TargetUrlMaxLength} characters.", nameof(targetUrl));

    CommitId = commitId;
    Status = status;
    TargetUrl = targetUrl;
    PostedAt = postedAt.ToUniversalTime();
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the commit.</summary>
  public long CommitId { get; }

  /// <summary>Gets the status that was published: its context, its state and its description.</summary>
  public CommitStatus Status { get; }

  /// <summary>Gets the address the status links to, or <see langword="null" />.</summary>
  public string? TargetUrl { get; }

  /// <summary>Gets when the status was published, in UTC.</summary>
  public DateTimeOffset PostedAt { get; }

  /// <summary>
  ///   Gets the identifier of the GitHub check run published in place of the status (M5.05), or
  ///   <see langword="null" /> when it was published as a status: a later notification updates that check run.
  /// </summary>
  public long? CheckRunId { get; init; }

  #endregion
}