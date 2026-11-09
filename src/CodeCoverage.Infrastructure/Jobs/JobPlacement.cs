// ------------------------------------------------------------------------------------------------
// <copyright file="JobPlacement.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>Where a job lands in the queue: its organization and its keys, prefixed by its lane.</summary>
/// <param name="OrganizationId">The organization of the job, or <see langword="null" /> when it stays out of any.</param>
/// <param name="LockKey">The prefixed key that serializes the job, or <see langword="null" />.</param>
/// <param name="DedupKey">The prefixed key that merges the job with its queued twin, or <see langword="null" />.</param>
/// <remarks>
///   The prefix is <c>o{id}:</c> for the job of an organization and <c>i:</c> for a job of the instance. The
///   unique indexes of the queue cover the whole key: the same business key in two lanes neither merges nor
///   serializes.
/// </remarks>
internal sealed record JobPlacement(long? OrganizationId, string? LockKey, string? DedupKey)
{
  #region Fields

  /// <summary>The prefix of the keys of a job that stays out of any organization.</summary>
  private const string InstancePrefix = "i:";

  #endregion

  #region Methods

  /// <summary>Places a request queued from a given scope.</summary>
  /// <param name="request">The request.</param>
  /// <param name="currentOrganizationId">
  ///   The organization the queuing scope has entered, or <see langword="null" /> when it has entered none.
  /// </param>
  /// <returns>The placement of the job.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">
  ///   The scope does not allow the organization of the request: see
  ///   <see cref="JobRequest.ResolveOrganizationId" />.
  /// </exception>
  public static JobPlacement Of(JobRequest request, long? currentOrganizationId)
  {
    ArgumentNullException.ThrowIfNull(request);

    var organizationId = request.ResolveOrganizationId(currentOrganizationId);
    var prefix = organizationId is { } id
                   ? string.Create(CultureInfo.InvariantCulture, $"o{id}:")
                   : InstancePrefix;

    return new JobPlacement(organizationId, Prefixed(prefix, request.LockKey), Prefixed(prefix, request.DedupKey));
  }

  /// <summary>Prefixes an optional key.</summary>
  /// <param name="prefix">The prefix of the lane.</param>
  /// <param name="key">The business key, or <see langword="null" />.</param>
  /// <returns>The prefixed key, or <see langword="null" /> when there is no key.</returns>
  private static string? Prefixed(string prefix, string? key) => key is null ? null : prefix + key;

  #endregion
}
