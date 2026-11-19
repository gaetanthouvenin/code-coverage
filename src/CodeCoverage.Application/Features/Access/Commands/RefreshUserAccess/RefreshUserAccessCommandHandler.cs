// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserAccessCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access.Commands.RefreshUserAccess;

/// <summary>Handler for the <see cref="RefreshUserAccessCommand" /> command.</summary>
/// <param name="users">The repository of the users.</param>
/// <param name="gateways">What resolves the token provider and the user gateway of the forge of a user.</param>
/// <param name="directory">The directory of the organizations, which gives the one of the scope.</param>
/// <param name="accesses">The cache of the accesses of the organization of the scope.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The repositories are those the forge of the user lists for it in the account of the organization, through the
///     neutral <see cref="IForgeUserGateway" />, with the permission it reports. Each listed repository is written with
///     the current date; a repository no longer listed loses its row.
///   </para>
///   <para>
///     It fails closed. When the user has no token its forge accepts any more, no longer reaches the account, or
///     belongs to another forge than the organization, its rows in the organization are removed. When the forge cannot
///     be asked, nothing is written: the rows keep their date and stop granting anything once
///     <c>Access:HardExpiryMinutes</c> has passed.
///   </para>
/// </remarks>
internal sealed class RefreshUserAccessCommandHandler(
  IUserRepository users,
  IForgeGatewayProvider gateways,
  IOrganizationDirectory directory,
  IUserRepositoryAccessRepository accesses,
  IClock clock) : ICommandHandler<RefreshUserAccessCommand>
{
  #region Methods

  /// <summary>Lists the repositories of the account of an organization that a user can reach.</summary>
  /// <param name="user">The user, whose token is renewed by the provider of its forge.</param>
  /// <param name="organization">The organization.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>
  ///   The repositories, empty when the user no longer reaches the account or belongs to another forge, or
  ///   <see langword="null" /> when the user has no token its forge accepts.
  /// </returns>
  private async Task<IReadOnlyList<ForgeRepositoryPermission>?> ListRepositoriesAsync(
    AppUser user,
    Organization organization,
    CancellationToken cancellationToken)
  {
    // The accounts of a forge never grant anything in an organization of another one.
    if (organization.Forge != user.Forge)
    {
      return [];
    }

    try
    {
      var token = await gateways.GetUserTokenProvider(user.Forge).GetTokenAsync(user.Id, cancellationToken).ConfigureAwait(false);
      var userGateway = gateways.GetUserGateway(user.Forge);
      var accounts = await userGateway.ListAccountsAsync(token, cancellationToken).ConfigureAwait(false);
      var accountId = Organization.NormalizeExternalAccountId(organization.ExternalAccountId);

      // The forge refuses every call made through a suspended access: it grants nothing.
      if (accounts.FirstOrDefault(a => !a.Suspended && Organization.NormalizeExternalAccountId(a.AccountId) == accountId) is not { } account)
      {
        return [];
      }

      return await userGateway.ListRepositoryPermissionsAsync(token, account, cancellationToken).ConfigureAwait(false);
    }
    catch (UnauthorizedException)
    {
      return null;
    }
  }

  #endregion

  #region Implementation of ICommandHandler<RefreshUserAccessCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(RefreshUserAccessCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (await users.FindAsync(command.UserId, cancellationToken).ConfigureAwait(false) is not { } user
        || await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false) is not { } organization)
    {
      return Unit.Value;
    }

    var listed = await ListRepositoriesAsync(user, organization, cancellationToken).ConfigureAwait(false) ?? [];
    var now = clock.UtcNow;
    var kept = new HashSet<string>(StringComparer.Ordinal);

    foreach (var repository in listed)
    {
      await accesses.UpsertAsync(user.Id, repository.RepositoryExternalId, repository.Permission, now, cancellationToken).ConfigureAwait(false);
      kept.Add(TrackedRepository.NormalizeExternalId(repository.RepositoryExternalId));
    }

    foreach (var stale in await accesses.ListOfUserAsync(user.Id, cancellationToken).ConfigureAwait(false))
    {
      if (!kept.Contains(stale.RepositoryExternalId))
      {
        await accesses.RemoveAsync(user.Id, stale.RepositoryExternalId, cancellationToken).ConfigureAwait(false);
      }
    }

    return Unit.Value;
  }

  #endregion
}
