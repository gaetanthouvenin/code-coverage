// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationRoleResolver.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Identity;

/// <summary>Tells the role of a user in an organization from its stored membership, failing closed.</summary>
/// <param name="memberships">The repository of the memberships.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="users">The repository of the users.</param>
/// <param name="options">The settings of the access, read at each call.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     Nothing is cached: the row is read and its age compared with <c>Access:HardExpiryMinutes</c> at each call,
///     so that a membership that is no longer refreshed, because its job died or the refresh token of the user
///     expired, stops granting anything by itself.
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
  IOptionsMonitor<AccessOptions> options,
  IClock clock) : IOrganizationRoleResolver
{
  #region Implementation of IOrganizationRoleResolver

  /// <inheritdoc />
  public async Task<OrganizationRole?> ResolveAsync(long userId, long organizationId, CancellationToken cancellationToken)
  {
    var membership = await memberships.FindAsync(userId, organizationId, cancellationToken).ConfigureAwait(false);
    var hardExpiry = TimeSpan.FromMinutes(Math.Max(0, options.CurrentValue.HardExpiryMinutes));

    if (membership is null || !membership.IsFresh(clock.UtcNow, hardExpiry))
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
