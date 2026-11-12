// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserAccessCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Access.Commands.RefreshUserAccess;

/// <summary>Handler for the <see cref="RefreshUserAccessCommand" /> command.</summary>
/// <param name="users">The repository of the users.</param>
/// <param name="tokens">What gives the token GitHub issued for a user.</param>
/// <param name="userGateway">The gateway that calls GitHub in the name of a user.</param>
/// <param name="directory">The directory of the organizations, which gives the one of the scope.</param>
/// <param name="accesses">The cache of the accesses of the organization of the scope.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The repositories are those GitHub lists for the user in the installation of the account of the organization,
///     with the permission it reports (<c>admin</c>, <c>push</c>, <c>pull</c>). Each listed repository is written with
///     the current date; a repository no longer listed loses its row.
///   </para>
///   <para>
///     It fails closed. When the user has no token GitHub accepts any more, or no longer reaches the installation, its
///     rows in the organization are removed. When GitHub cannot be asked, nothing is written: the rows keep their date
///     and stop granting anything once <c>Access:HardExpiryMinutes</c> has passed.
///   </para>
/// </remarks>
internal sealed class RefreshUserAccessCommandHandler(
  IUserRepository users,
  IGitHubUserTokenProvider tokens,
  IGitHubUserGateway userGateway,
  IOrganizationDirectory directory,
  IUserRepositoryAccessRepository accesses,
  IClock clock) : ICommandHandler<RefreshUserAccessCommand>
{
  #region Methods

  /// <summary>Lists the repositories of the installation of an organization that a user can reach.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="organization">The organization.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>
  ///   The repositories, empty when the user no longer reaches the installation, or <see langword="null" /> when the user
  ///   has no token GitHub accepts.
  /// </returns>
  private async Task<IReadOnlyList<GitHubUserRepository>?> ListRepositoriesAsync(
    long userId,
    Organization organization,
    CancellationToken cancellationToken)
  {
    try
    {
      var token = await tokens.GetTokenAsync(userId, cancellationToken).ConfigureAwait(false);
      var installations = await userGateway.ListInstallationsAsync(token, cancellationToken).ConfigureAwait(false);
      var accountId = Organization.NormalizeExternalAccountId(organization.ExternalAccountId);

      // GitHub refuses every call made for a suspended installation: it grants nothing.
      if (installations.FirstOrDefault(i => !i.Suspended && Organization.NormalizeExternalAccountId(i.AccountId) == accountId) is not { } installation)
      {
        return [];
      }

      return await userGateway.ListInstallationRepositoriesAsync(token, installation.Id, cancellationToken).ConfigureAwait(false);
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

    var listed = await ListRepositoriesAsync(user.Id, organization, cancellationToken).ConfigureAwait(false) ?? [];
    var now = clock.UtcNow;
    var kept = new HashSet<string>(StringComparer.Ordinal);

    foreach (var repository in listed)
    {
      await accesses.UpsertAsync(user.Id, repository.ExternalId, repository.Permission, now, cancellationToken).ConfigureAwait(false);
      kept.Add(TrackedRepository.NormalizeExternalId(repository.ExternalId));
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
