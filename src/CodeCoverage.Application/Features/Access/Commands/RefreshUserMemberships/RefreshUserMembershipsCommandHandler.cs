// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserMembershipsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access.Commands.RefreshUserMemberships;

/// <summary>Handler for the <see cref="RefreshUserMembershipsCommand" /> command.</summary>
/// <param name="users">The repository of the users.</param>
/// <param name="gateways">What resolves the token provider and the user gateway of the forge of a user.</param>
/// <param name="updater">What writes the memberships of a user.</param>
/// <param name="jobQueue">The job queue.</param>
/// <remarks>
///   <para>
///     The rule of the memberships is the one of <see cref="MembershipUpdater" />, shared with the sign-in.
///   </para>
///   <para>
///     It fails closed. When the forge of the user cannot be asked, nothing is written: the rows keep their date and
///     stop granting anything once <c>Access:HardExpiryMinutes</c> has passed. When the user has no token its forge
///     accepts any more, its memberships are removed at once.
///   </para>
///   <para>
///     It reads and writes global tables only, so it runs outside any organization. For each organization the user is
///     still linked to, it queues a <c>RefreshUserAccess</c> job in that organization: the access to the repositories
///     is an organization table, refreshed inside it (ADR-013). The jobs are queued once the memberships are saved; a
///     job lost in between waits for the next refresh, and the rows it would have written grant nothing meanwhile.
///   </para>
/// </remarks>
internal sealed class RefreshUserMembershipsCommandHandler(
  IUserRepository users,
  IForgeGatewayProvider gateways,
  MembershipUpdater updater,
  IJobQueue jobQueue) : ICommandHandler<RefreshUserMembershipsCommand>
{
  #region Methods

  /// <summary>Lists the accounts the user reaches through the access its forge gives this instance.</summary>
  /// <param name="user">The user, whose token is renewed by the provider of its forge.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The accounts, or <see langword="null" /> when the user has no token its forge accepts.</returns>
  private async Task<IReadOnlyList<ForgeUserAccount>?> ListAccountsAsync(AppUser user, CancellationToken cancellationToken)
  {
    try
    {
      var token = await gateways.GetUserTokenProvider(user.Forge).GetTokenAsync(user.Id, cancellationToken).ConfigureAwait(false);

      return await gateways.GetUserGateway(user.Forge).ListAccountsAsync(token, cancellationToken).ConfigureAwait(false);
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

    var accounts = await ListAccountsAsync(user, cancellationToken).ConfigureAwait(false);

    var roles = await updater.ReplaceAsync(user, accounts, cancellationToken).ConfigureAwait(false);

    foreach (var organizationId in roles.Keys.Order())
    {
      var job = JobRequest.ForOrganization(
        organizationId,
        JobTypes.RefreshUserAccess,
        RefreshUserAccessJobs.Payload(user.Id),
        RefreshUserAccessJobs.LockKey(user.Id),
        RefreshUserAccessJobs.DedupKey(user.Id),
        coalesce: true
      );

      await jobQueue.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
