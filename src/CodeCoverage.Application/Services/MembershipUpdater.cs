// ------------------------------------------------------------------------------------------------
// <copyright file="MembershipUpdater.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Services;

/// <summary>
///   Writes which organizations a user is linked to, and with which role, from the accounts the user reaches on its
///   forge: the rule the sign-in and the <c>RefreshUserMemberships</c> job share.
/// </summary>
/// <param name="forges">The provider of the gateways of the forges, which gives the directory of the forge of the user.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="memberships">The repository of the memberships.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     A user is linked to the organizations of its forge whose account it reaches, as long as the organization is active
///     here: a pending or suspended organization gets no row. The role is decided by <see cref="OrganizationRoles.Of" />,
///     from the membership the <see cref="IForgeDirectoryGateway" /> of the forge reads through the connection the account
///     is reached by. A membership that is no longer found is removed.
///   </para>
///   <para>
///     It fails closed: an organization that refuses to tell the membership gets no row, with a warning, and the others
///     are written all the same. It reads and writes global tables only, so it runs outside any organization.
///   </para>
/// </remarks>
internal sealed partial class MembershipUpdater(
  IForgeGatewayProvider forges,
  IOrganizationDirectory directory,
  IOrganizationMembershipRepository memberships,
  IClock clock,
  ILogger<MembershipUpdater> logger)
{
  #region Methods

  /// <summary>Replaces the memberships of a user.</summary>
  /// <param name="user">The user, saved.</param>
  /// <param name="accounts">
  ///   The accounts the forge of the user lists for it, or <see langword="null" /> when the user has no token its forge
  ///   accepts any more: every membership is then removed.
  /// </param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The role of the user in each organization it is linked to, by identifier of the organization.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="user" /> is <see langword="null" />.</exception>
  public async Task<IReadOnlyDictionary<long, OrganizationRole>> ReplaceAsync(
    AppUser user,
    IReadOnlyList<ForgeUserAccount>? accounts,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    var roles = new Dictionary<long, OrganizationRole>();

    // The forge refuses every call made through a suspended access: it links nobody.
    foreach (var account in (accounts ?? []).Where(static a => !a.Suspended))
    {
      var organization = await directory.FindByAccountIdAsync(user.Forge, account.AccountId, cancellationToken).ConfigureAwait(false);

      if (organization is not { State: OrganizationState.Active })
      {
        continue;
      }

      OrganizationRole role;

      try
      {
        role = await ResolveRoleAsync(user, account, organization, cancellationToken).ConfigureAwait(false);
      }
      catch (ForbiddenException)
      {
        // Fail closed for this organization alone: no row, and the others go on.
        LogMembershipRefused(logger, user.Forge, user.Id, organization.Login);

        continue;
      }

      // Two accesses to the same account cannot be told apart here: the highest role is kept.
      roles[organization.Id] = roles.TryGetValue(organization.Id, out var known) && known is OrganizationRole.Admin ? known : role;
    }

    await memberships.ReplaceAsync(user.Id, roles, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return roles;
  }

  /// <summary>Decides the role of the user in the organization of one account.</summary>
  /// <param name="user">The user.</param>
  /// <param name="account">The account, as the forge lists it for the user.</param>
  /// <param name="organization">The organization of the account, active.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The role.</returns>
  private async Task<OrganizationRole> ResolveRoleAsync(
    AppUser user,
    ForgeUserAccount account,
    Organization organization,
    CancellationToken cancellationToken)
  {
    // A personal account has no membership: its owner administers it, anyone else takes part.
    var membership = organization.AccountType is OrganizationAccountType.Organization
                       ? await forges.GetDirectory(user.Forge)
                                     .GetMembershipAsync(
                                       new ForgeConnectionRef(user.Forge, account.ConnectionId),
                                       new ForgeAccountInfo(organization.ExternalAccountId, organization.Login),
                                       new ForgeUser(user.ExternalId, user.Login, user.AvatarUrl),
                                       cancellationToken
                                     )
                                     .ConfigureAwait(false)
                       : null;

    return OrganizationRoles.Of(user, organization, membership);
  }

  [LoggerMessage(
    EventId = 1600,
    Level = LogLevel.Warning,
    Message = "{Forge} refused to tell the membership of user {UserId} in {Organization}: no membership is kept for it"
  )]
  private static partial void LogMembershipRefused(ILogger logger, Forge forge, long userId, string organization);

  #endregion
}
