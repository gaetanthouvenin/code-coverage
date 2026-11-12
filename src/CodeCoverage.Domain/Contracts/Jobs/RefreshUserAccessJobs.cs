// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserAccessJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the refresh of what a user may do on the repositories of one organization.</summary>
/// <remarks>
///   The job belongs to one organization, the one of its lane: its lock runs one refresh per user at a time, and a
///   refresh already waiting absorbs a new one, since the job asks the forge and carries nothing else (ARCHITECTURE §6.1,
///   <c>user:{id}</c> and <c>access:{id}</c>, prefixed by the lane).
/// </remarks>
public static class RefreshUserAccessJobs
{
  #region Methods

  /// <summary>Builds the refresh for the organization of the current scope.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <returns>The job, merged with the refresh already waiting for the same user.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is zero or negative.</exception>
  public static JobRequest Create(long userId) =>
    JobRequest.Create(JobTypes.RefreshUserAccess, Payload(userId), LockKey(userId), DedupKey(userId), coalesce: true);

  /// <summary>Builds the payload of the job.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <returns>The payload.</returns>
  /// <remarks>
  ///   With <see cref="LockKey" /> and <see cref="DedupKey" />, it lets the refresh of the memberships queue the job in
  ///   an organization it names, the one place allowed to (ADR-013).
  /// </remarks>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is zero or negative.</exception>
  public static string Payload(long userId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

    return JsonSerializer.Serialize(new { UserId = userId });
  }

  /// <summary>Builds the lock key of the job: one refresh per user at a time.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <returns>The key, without the prefix of the lane.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is zero or negative.</exception>
  public static string LockKey(long userId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

    return string.Create(CultureInfo.InvariantCulture, $"user:{userId}");
  }

  /// <summary>Builds the deduplication key of the job: a refresh already waiting absorbs a new one.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <returns>The key, without the prefix of the lane.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is zero or negative.</exception>
  public static string DedupKey(long userId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

    return string.Create(CultureInfo.InvariantCulture, $"access:{userId}");
  }

  #endregion
}
