// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserMembershipsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Logging;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access.Commands.RefreshUserMemberships;

/// <summary>Handler for the <see cref="RefreshUserMembershipsCommand" /> command.</summary>
/// <param name="users">The repository of the users.</param>
/// <param name="tokens">What gives the token GitHub issued for a user.</param>
/// <param name="userGateway">The gateway that calls GitHub in the name of a user.</param>
/// <param name="appGateway">The gateway of the App.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="memberships">The repository of the memberships.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     A user is linked to the organizations whose installation of the App it can reach on GitHub, as long as the
///     organization is active here: a pending or suspended organization gets no row. The role is decided by
///     <see cref="OrganizationRoles.Of" />. A membership that is no longer found is removed.
///   </para>
///   <para>
///     It fails closed. When GitHub cannot be asked, nothing is written: the rows keep their date and stop granting
///     anything once <c>Access:HardExpiryMinutes</c> has passed. When the user has no token GitHub accepts any
///     more, its memberships are removed at once. An organization that refuses to tell the membership gets no row,
///     with a warning; the others are refreshed all the same.
///   </para>
///   <para>It reads and writes global tables only, so it runs outside any organization.</para>
/// </remarks>
internal sealed partial class RefreshUserMembershipsCommandHandler(
  IUserRepository users,
  IGitHubUserTokenProvider tokens,
  IGitHubUserGateway userGateway,
  IGitHubAppGateway appGateway,
  IOrganizationDirectory directory,
  IOrganizationMembershipRepository memberships,
  IClock clock,
  ILogger<RefreshUserMembershipsCommandHandler> logger) : ICommandHandler<RefreshUserMembershipsCommand>
{
  #region Methods

  /// <summary>Lists the installations of the App the user can reach.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The installations, or <see langword="null" /> when the user has no token GitHub accepts.</returns>
  private async Task<IReadOnlyList<GitHubInstallationInfo>?> ListInstallationsAsync(long userId, CancellationToken cancellationToken)
  {
    try
    {
      var token = await tokens.GetTokenAsync(userId, cancellationToken).ConfigureAwait(false);

      return await userGateway.ListInstallationsAsync(token, cancellationToken).ConfigureAwait(false);
    }
    catch (UnauthorizedException)
    {
      return null;
    }
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

  #region Implementation of ICommandHandler<RefreshUserMembershipsCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(RefreshUserMembershipsCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (await users.FindAsync(command.UserId, cancellationToken).ConfigureAwait(false) is not { } user)
    {
      return Unit.Value;
    }

    var roles = new Dictionary<long, OrganizationRole>();

    if (await ListInstallationsAsync(user.Id, cancellationToken).ConfigureAwait(false) is { } installations)
    {
      // GitHub refuses every call made for a suspended installation: it links nobody.
      foreach (var installation in installations.Where(static i => !i.Suspended))
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
    }

    await memberships.ReplaceAsync(user.Id, roles, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
