// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserMembershipsJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;

namespace CodeCoverage.Domain.Contracts.Jobs;

/// <summary>Builds the job that refreshes the memberships of a user, which runs on the lane of the instance.</summary>
public static class RefreshUserMembershipsJobs
{
  #region Methods

  /// <summary>Builds the refresh of the memberships of one user.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <returns>The job, merged with the refresh already waiting for the same user.</returns>
  /// <remarks>
  ///   The memberships of a user span several organizations: the job belongs to none. Merging loses nothing, since
  ///   the job asks the forge and carries nothing of the event that queued it. Its lock runs one refresh per user at
  ///   a time.
  /// </remarks>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is zero or negative.</exception>
  public static JobRequest Create(long userId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

    var key = string.Create(CultureInfo.InvariantCulture, $"memberships:{userId}");

    return JobRequest.ForInstance(JobTypes.RefreshUserMemberships, JsonSerializer.Serialize(new { UserId = userId }), key, key, coalesce: true);
  }

  #endregion
}
