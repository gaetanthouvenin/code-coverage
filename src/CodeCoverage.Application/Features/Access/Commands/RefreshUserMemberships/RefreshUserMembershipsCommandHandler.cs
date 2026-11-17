// ------------------------------------------------------------------------------------------------
// <copyright file="RefreshUserMembershipsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access.Commands.RefreshUserMemberships;

/// <summary>Handler for the <see cref="RefreshUserMembershipsCommand" /> command.</summary>
/// <param name="users">The repository of the users.</param>
/// <param name="gateways">What resolves the provider of the token the forge of a user issued for it.</param>
/// <param name="userGateway">The gateway that calls GitHub in the name of a user.</param>
/// <param name="updater">What writes the memberships of a user.</param>
/// <param name="jobQueue">The job queue.</param>
/// <remarks>
///   <para>
///     The rule of the memberships is the one of <see cref="MembershipUpdater" />, shared with the sign-in.
///   </para>
///   <para>
///     It fails closed. When GitHub cannot be asked, nothing is written: the rows keep their date and stop granting
///     anything once <c>Access:HardExpiryMinutes</c> has passed. When the user has no token GitHub accepts any
///     more, its memberships are removed at once.
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
  IGitHubUserGateway userGateway,
  MembershipUpdater updater,
  IJobQueue jobQueue) : ICommandHandler<RefreshUserMembershipsCommand>
{
  #region Methods

  /// <summary>Lists the installations of the App the user can reach.</summary>
  /// <param name="user">The user, whose token is renewed by the provider of its forge.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The installations, or <see langword="null" /> when the user has no token GitHub accepts.</returns>
  private async Task<IReadOnlyList<GitHubInstallationInfo>?> ListInstallationsAsync(AppUser user, CancellationToken cancellationToken)
  {
    try
    {
      var token = await gateways.GetUserTokenProvider(user.Forge).GetTokenAsync(user.Id, cancellationToken).ConfigureAwait(false);

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

    var installations = await ListInstallationsAsync(user, cancellationToken).ConfigureAwait(false);

    var roles = await updater.ReplaceAsync(user, installations, cancellationToken).ConfigureAwait(false);

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
