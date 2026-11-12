// ------------------------------------------------------------------------------------------------
// <copyright file="MembershipUpdater.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Services;

/// <summary>
///   Writes which organizations a user is linked to, and with which role, from the installations of the App the user
///   reaches on GitHub: the rule the sign-in and the <c>RefreshUserMemberships</c> job share.
/// </summary>
/// <param name="appGateway">The gateway of the App.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="memberships">The repository of the memberships.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     A user is linked to the organizations whose installation it can reach, as long as the organization is active
///     here: a pending or suspended organization gets no row. The role is decided by <see cref="OrganizationRoles.Of" />.
///     A membership that is no longer found is removed.
///   </para>
///   <para>
///     It fails closed: an organization that refuses to tell the membership gets no row, with a warning, and the others
///     are written all the same. It reads and writes global tables only, so it runs outside any organization.
///   </para>
/// </remarks>
internal sealed partial class MembershipUpdater(
  IGitHubAppGateway appGateway,
  IOrganizationDirectory directory,
  IOrganizationMembershipRepository memberships,
  IClock clock,
  ILogger<MembershipUpdater> logger)
{
  #region Methods

  /// <summary>Replaces the memberships of a user.</summary>
  /// <param name="user">The user, saved.</param>
  /// <param name="installations">
  ///   The installations GitHub lists for the user, or <see langword="null" /> when the user has no token GitHub accepts
  ///   any more: every membership is then removed.
  /// </param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The role of the user in each organization it is linked to, by identifier of the organization.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="user" /> is <see langword="null" />.</exception>
  public async Task<IReadOnlyDictionary<long, OrganizationRole>> ReplaceAsync(
    AppUser user,
    IReadOnlyList<GitHubInstallationInfo>? installations,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    var roles = new Dictionary<long, OrganizationRole>();

    // GitHub refuses every call made for a suspended installation: it links nobody.
    foreach (var installation in (installations ?? []).Where(static i => !i.Suspended))
    {
      var organization = await directory.FindByAccountIdAsync(Forge.GitHub, installation.AccountId, cancellationToken).ConfigureAwait(false);

      if (organization is not { State: OrganizationState.Active })
      {
        continue;
      }

      OrganizationRole role;

      try
      {
        role = await ResolveRoleAsync(user, installation, organization, cancellationToken).ConfigureAwait(false);
      }
      catch (ForbiddenException)
      {
        // Fail closed for this organization alone: no row, and the others go on.
        LogMembershipRefused(logger, user.Id, organization.Login);

        continue;
      }

      // Two installations of the same account cannot be told apart here: the highest role is kept.
      roles[organization.Id] = roles.TryGetValue(organization.Id, out var known) && known is OrganizationRole.Admin ? known : role;
    }

    await memberships.ReplaceAsync(user.Id, roles, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return roles;
  }

  /// <summary>Decides the role of the user in the organization of one installation.</summary>
  /// <param name="user">The user.</param>
  /// <param name="installation">The installation, as GitHub lists it for the user.</param>
  /// <param name="organization">The organization of its account, active.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The role.</returns>
  private async Task<OrganizationRole> ResolveRoleAsync(
    AppUser user,
    GitHubInstallationInfo installation,
    Organization organization,
    CancellationToken cancellationToken)
  {
    // GitHub has no membership for a personal account: its owner administers it, anyone else takes part.
    var membership = organization.AccountType is OrganizationAccountType.Organization
                       ? await appGateway.GetOrganizationMembershipAsync(installation.Id, organization.Login, user.Login, cancellationToken).ConfigureAwait(false)
                       : null;

    return OrganizationRoles.Of(user, organization, membership);
  }

  [LoggerMessage(
    EventId = 1600,
    Level = LogLevel.Warning,
    Message = "GitHub refused to tell the membership of user {UserId} in {Organization}: no membership is kept for it"
  )]
  private static partial void LogMembershipRefused(ILogger logger, long userId, string organization);

  #endregion
}
