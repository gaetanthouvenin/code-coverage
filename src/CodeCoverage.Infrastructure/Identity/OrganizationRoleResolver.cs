// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRoleResolver.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Identity;

/// <summary>Tells the role of a user in an organization from its stored membership, failing closed.</summary>
/// <param name="memberships">The repository of the memberships.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="users">The repository of the users.</param>
/// <param name="jobQueue">The job queue, which receives the refreshes of the memberships.</param>
/// <param name="options">The settings of the access, read at each call.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     Nothing is cached: the row is read and its age compared with <c>Access:HardExpiryMinutes</c> at each call,
///     so that a membership that is no longer refreshed, because its job died or the refresh token of the user
///     expired, stops granting anything by itself.
///   </para>
///   <para>
///     A membership older than <c>Access:CacheMinutes</c> queues the refresh of the memberships of its user, merged
///     with the one already waiting (<c>memberships:{id}</c>) and queued once per scope, as the accesses to the
///     repositories do: the request does no work of the forge, and the next one reads the refreshed rows. Without it,
///     a membership is refreshed only at the sign-in and by the webhooks, and every organization disappears once the
///     hard expiry has passed, while the cookie lives on.
///   </para>
///   <para>
///     The owner of a personal account administers it: GitHub has no membership for such an account. The link
///     must still be fresh, as any other.
///   </para>
/// </remarks>
internal sealed class OrganizationRoleResolver(
  IOrganizationMembershipRepository memberships,
  IOrganizationDirectory directory,
  IUserRepository users,
  IJobQueue jobQueue,
  IOptionsMonitor<AccessOptions> options,
  IClock clock) : IOrganizationRoleResolver
{
  #region Fields

  private readonly HashSet<long> _refreshed = [];

  #endregion

  #region Methods

  /// <summary>Queues the refresh of the memberships of a user whose membership is stale, once per scope.</summary>
  /// <param name="membership">The membership read.</param>
  /// <param name="now">The current time.</param>
  /// <param name="cache">How long a membership answers without a refresh.</param>
  /// <param name="cancellationToken">A token to cancel the queueing.</param>
  /// <returns>A task that completes when the refresh is queued, or is not needed.</returns>
  private async Task RefreshIfStaleAsync(OrganizationMembership membership, DateTimeOffset now, TimeSpan cache, CancellationToken cancellationToken)
  {
    if (now - membership.RefreshedAt >= cache && _refreshed.Add(membership.UserId))
    {
      await jobQueue.TryEnqueueAsync(RefreshUserMembershipsJobs.Create(membership.UserId), cancellationToken).ConfigureAwait(false);
    }
  }

  #endregion

  #region Implementation of IOrganizationRoleResolver

  /// <inheritdoc />
  public async Task<OrganizationRole?> ResolveAsync(long userId, long organizationId, CancellationToken cancellationToken)
  {
    var membership = await memberships.FindAsync(userId, organizationId, cancellationToken).ConfigureAwait(false);
    var settings = options.CurrentValue;
    var now = clock.UtcNow;
    var hardExpiry = TimeSpan.FromMinutes(Math.Max(0, settings.HardExpiryMinutes));

    if (membership is null)
    {
      return null;
    }

    await RefreshIfStaleAsync(membership, now, TimeSpan.FromMinutes(Math.Max(0, settings.CacheMinutes)), cancellationToken).ConfigureAwait(false);

    if (!membership.IsFresh(now, hardExpiry))
    {
      return null;
    }

    var organization = await directory.FindAsync(organizationId, cancellationToken).ConfigureAwait(false);

    if (organization is not { State: OrganizationState.Active })
    {
      return null;
    }

    if (organization.AccountType is OrganizationAccountType.User
        && await users.FindAsync(userId, cancellationToken).ConfigureAwait(false) is { } user
        && OrganizationRoles.Owns(user, organization))
    {
      return OrganizationRole.Admin;
    }

    return membership.Role;
  }

  #endregion
}
