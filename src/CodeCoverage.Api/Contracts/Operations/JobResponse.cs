// ------------------------------------------------------------------------------------------------
// <copyright file="JobResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Operations;

/// <summary>A background job: metadata only, never its payload, its keys or the text of its error.</summary>
/// <param name="Id">The identifier of the job.</param>
/// <param name="OrganizationId">The organization of its lane, or <see langword="null" /> on the lane of the instance.</param>
/// <param name="Type">The type of the job.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Attempts">How many times a worker claimed it.</param>
/// <param name="MaxAttempts">How many attempts it gets.</param>
/// <param name="RunAfter">When it may run (next), in UTC.</param>
/// <param name="FirstQueuedAt">When it was first queued, in UTC.</param>
/// <param name="LockedBy">The worker that holds it, or <see langword="null" />.</param>
/// <param name="LockedUntil">When the lease of the worker ends, or <see langword="null" />.</param>
/// <param name="HasError">Whether its last attempt failed.</param>
/// <param name="CorrelationId">The correlation identifier of the request that queued it.</param>
/// <param name="CreatedAt">When it was created, in UTC.</param>
/// <param name="CompletedAt">When it ended, or <see langword="null" />.</param>
public sealed record JobResponse(
  long Id,
  long? OrganizationId,
  string Type,
  string State,
  int Attempts,
  int MaxAttempts,
  DateTimeOffset RunAfter,
  DateTimeOffset FirstQueuedAt,
  string? LockedBy,
  DateTimeOffset? LockedUntil,
  bool HasError,
  string CorrelationId,
  DateTimeOffset CreatedAt,
  DateTimeOffset? CompletedAt);
