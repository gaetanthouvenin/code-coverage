// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryAccessService.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Infrastructure.Identity;

/// <summary>Tells what a user may do on a repository of the organization of the scope, from the cache of GitHub.</summary>
/// <param name="accesses">The cache of the accesses of the organization of the scope.</param>
/// <param name="jobQueue">The job queue, which receives the refreshes.</param>
/// <param name="options">The settings of the access, read at each call.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     A row younger than <c>Access:CacheMinutes</c> answers alone. An older one still answers while it is younger than
///     <c>Access:HardExpiryMinutes</c>, and queues a refresh in the organization of the scope; past the hard expiry, it
///     grants nothing. A missing row grants nothing, and queues a refresh only when no row of the user in the
///     organization is fresh: a refresh that ran a moment ago and wrote no row for the repository already answered.
///   </para>
///   <para>
///     The refresh is merged with the one already waiting (<c>access:{id}</c>), so that a page that asks many times queues
///     a single job. The rows read are kept for the scope: the gate asks for read then administration, and reads once.
///     Nothing outlives the scope, no process-wide cache is kept.
///   </para>
/// </remarks>
internal sealed class RepositoryAccessService(
  IUserRepositoryAccessRepository accesses,
  IJobQueue jobQueue,
  IOptionsMonitor<AccessOptions> options,
  IClock clock) : IRepositoryAccessService
{
  #region Fields

  private readonly Dictionary<(long UserId, string RepositoryExternalId), RepositoryAccess?> _read = [];

  private readonly HashSet<long> _refreshed = [];

  #endregion

  #region Methods

  /// <summary>Reads the access of a user to a repository, once per scope.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="repositoryExternalId">The identifier the forge gives the repository, normalized.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The access, or <see langword="null" />.</returns>
  private async Task<RepositoryAccess?> FindAsync(long userId, string repositoryExternalId, CancellationToken cancellationToken)
  {
    if (!_read.TryGetValue((userId, repositoryExternalId), out var access))
    {
      access = await accesses.FindAsync(userId, repositoryExternalId, cancellationToken).ConfigureAwait(false);
      _read[(userId, repositoryExternalId)] = access;
    }

    return access;
  }

  /// <summary>Queues the refresh of the accesses of a user, once per scope.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the queueing.</param>
  /// <returns>A task that completes when the refresh is queued.</returns>
  private async Task RefreshAsync(long userId, CancellationToken cancellationToken)
  {
    if (_refreshed.Add(userId))
    {
      await jobQueue.TryEnqueueAsync(RefreshUserAccessJobs.Create(userId), cancellationToken).ConfigureAwait(false);
    }
  }

  #endregion

  #region Implementation of IRepositoryAccessService

  /// <inheritdoc />
  public async Task<bool> CanAsync(long userId, string repositoryExternalId, RepositoryPermission permission, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repositoryExternalId);

    var settings = options.CurrentValue;
    var now = clock.UtcNow;
    var cache = TimeSpan.FromMinutes(Math.Max(0, settings.CacheMinutes));
    var hardExpiry = TimeSpan.FromMinutes(Math.Max(0, settings.HardExpiryMinutes));
    var access = await FindAsync(userId, TrackedRepository.NormalizeExternalId(repositoryExternalId), cancellationToken).ConfigureAwait(false);

    if (access is null)
    {
      var known = await accesses.ListOfUserAsync(userId, cancellationToken).ConfigureAwait(false);

      if (!known.Any(a => now - a.RefreshedAt < cache))
      {
        await RefreshAsync(userId, cancellationToken).ConfigureAwait(false);
      }

      return false;
    }

    var age = now - access.RefreshedAt;

    if (age >= cache)
    {
      await RefreshAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    return age < hardExpiry && access.Permission >= permission;
  }

  #endregion
}
