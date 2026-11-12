// ------------------------------------------------------------------------------------------------
// <copyright file="JobSummary.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Domain.Operations;

/// <summary>A background job as the administrators see it: a read model of the job queue.</summary>
/// <remarks>
///   Built by <see cref="Ports.IJobMonitor" /> from the <c>Jobs</c> table. It never carries the payload, the keys or
///   the text of the last error of the job: they may name private repositories or files, or hold data of an
///   organization, and the screens do not need them (M3.13).
/// </remarks>
public sealed class JobSummary
{
  #region Properties

  /// <summary>Gets the identifier of the job.</summary>
  public required long Id { get; init; }

  /// <summary>Gets the organization of the lane of the job, or <see langword="null" /> on the lane of the instance.</summary>
  public long? OrganizationId { get; init; }

  /// <summary>Gets the type of the job, one of <see cref="JobTypes" />.</summary>
  public required string Type { get; init; }

  /// <summary>Gets where the job stands.</summary>
  public required JobState State { get; init; }

  /// <summary>Gets how many times a worker claimed the job.</summary>
  public required int Attempts { get; init; }

  /// <summary>Gets how many attempts the job gets before it is <see cref="JobState.Dead" />.</summary>
  public required int MaxAttempts { get; init; }

  /// <summary>Gets when the job may run (next), in UTC.</summary>
  public required DateTimeOffset RunAfter { get; init; }

  /// <summary>Gets when the job was first queued, in UTC; the start of its debounce window.</summary>
  public required DateTimeOffset FirstQueuedAt { get; init; }

  /// <summary>Gets the worker that holds the job, or <see langword="null" /> when none does.</summary>
  public string? LockedBy { get; init; }

  /// <summary>Gets when the lease of the worker ends, in UTC, or <see langword="null" />.</summary>
  public DateTimeOffset? LockedUntil { get; init; }

  /// <summary>Gets a value indicating whether the last attempt failed; its text is not exposed.</summary>
  public required bool HasError { get; init; }

  /// <summary>Gets the correlation identifier of the request that queued the job.</summary>
  public required string CorrelationId { get; init; }

  /// <summary>Gets when the job was created, in UTC.</summary>
  public required DateTimeOffset CreatedAt { get; init; }

  /// <summary>Gets when the job ended, in UTC, or <see langword="null" /> while it is not over.</summary>
  public DateTimeOffset? CompletedAt { get; init; }

  #endregion
}
