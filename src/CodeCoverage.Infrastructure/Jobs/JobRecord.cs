// ------------------------------------------------------------------------------------------------
// <copyright file="JobRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>A background job: one row of the <c>Jobs</c> table.</summary>
/// <remarks>
///   <para>
///     A technical record of the Infrastructure, never seen by the Application: the jobs are queued from
///     a <see cref="JobRequest" /> and read back as <see cref="JobSummary" />.
///   </para>
///   <para>
///     Two unique indexes, filtered by state, carry the rules of the queue: one queued job at most per
///     <see cref="DedupKey" />, one running job at most per <see cref="LockKey" />.
///   </para>
///   <para>
///     An operational table, without query filter or foreign key: the workers claim the jobs before they
///     enter an organization.
///   </para>
/// </remarks>
internal sealed class JobRecord
{
  #region Fields

  /// <summary>The maximum length of <see cref="LockKey" /> and <see cref="DedupKey" />, their prefix included.</summary>
  public const int KeyMaxLength = 200;

  /// <summary>The maximum length of <see cref="LockedBy" />.</summary>
  public const int LockedByMaxLength = 128;

  /// <summary>The maximum length of <see cref="CorrelationId" />.</summary>
  public const int CorrelationIdMaxLength = 64;

  #endregion

  #region Properties

  /// <summary>Gets or sets the identifier of the job, assigned when it is saved.</summary>
  public long Id { get; set; }

  /// <summary>
  ///   Gets or sets the organization the job runs in, or <see langword="null" /> when it stays out of any.
  /// </summary>
  public long? OrganizationId { get; set; }

  /// <summary>Gets or sets the type of the job, one of <see cref="JobTypes" />.</summary>
  public required string Type { get; set; }

  /// <summary>Gets or sets the payload of the job, as JSON.</summary>
  public required string PayloadJson { get; set; }

  /// <summary>Gets or sets the key that serializes the job, prefixed by its lane, or <see langword="null" />.</summary>
  public string? LockKey { get; set; }

  /// <summary>
  ///   Gets or sets the key that merges the job with its queued twin, prefixed by its lane, or
  ///   <see langword="null" />.
  /// </summary>
  public string? DedupKey { get; set; }

  /// <summary>Gets or sets where the job stands.</summary>
  public JobState State { get; set; }

  /// <summary>Gets or sets how many times a worker claimed the job.</summary>
  public int Attempts { get; set; }

  /// <summary>Gets or sets how many attempts the job gets before it is dead.</summary>
  public int MaxAttempts { get; set; }

  /// <summary>Gets or sets when the job may run (next), in UTC.</summary>
  public DateTimeOffset RunAfter { get; set; }

  /// <summary>Gets or sets when the job was first queued, in UTC: the start of its debounce window.</summary>
  public DateTimeOffset FirstQueuedAt { get; set; }

  /// <summary>Gets or sets the worker that holds the job, or <see langword="null" />.</summary>
  public string? LockedBy { get; set; }

  /// <summary>Gets or sets when the lease of the worker ends, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? LockedUntil { get; set; }

  /// <summary>Gets or sets the error of the last failed attempt, or <see langword="null" />.</summary>
  public string? LastError { get; set; }

  /// <summary>Gets or sets the correlation identifier of the request that queued the job.</summary>
  public required string CorrelationId { get; set; }

  /// <summary>Gets or sets when the job was created, in UTC.</summary>
  public DateTimeOffset CreatedAt { get; set; }

  /// <summary>Gets or sets when the job ended, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? CompletedAt { get; set; }

  #endregion
}
