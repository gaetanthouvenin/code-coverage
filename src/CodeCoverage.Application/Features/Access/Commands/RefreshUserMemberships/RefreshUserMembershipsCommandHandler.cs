// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserMembershipsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Access.Commands.RefreshUserMemberships;

/// <summary>Handler for the <see cref="RefreshUserMembershipsCommand" /> command.</summary>
/// <param name="users">The repository of the users.</param>
/// <param name="tokens">What gives the token GitHub issued for a user.</param>
/// <param name="userGateway">The gateway that calls GitHub in the name of a user.</param>
/// <param name="updater">What writes the memberships of a user.</param>
/// <remarks>
///   <para>
///     The rule of the memberships is the one of <see cref="MembershipUpdater" />, shared with the sign-in.
///   </para>
///   <para>
///     It fails closed. When GitHub cannot be asked, nothing is written: the rows keep their date and stop granting
///     anything once <c>Access:HardExpiryMinutes</c> has passed. When the user has no token GitHub accepts any
///     more, its memberships are removed at once.
///   </para>
///   <para>It reads and writes global tables only, so it runs outside any organization.</para>
/// </remarks>
internal sealed class RefreshUserMembershipsCommandHandler(
  IUserRepository users,
  IGitHubUserTokenProvider tokens,
  IGitHubUserGateway userGateway,
  MembershipUpdater updater) : ICommandHandler<RefreshUserMembershipsCommand>
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

    var installations = await ListInstallationsAsync(user.Id, cancellationToken).ConfigureAwait(false);

    await updater.ReplaceAsync(user, installations, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
