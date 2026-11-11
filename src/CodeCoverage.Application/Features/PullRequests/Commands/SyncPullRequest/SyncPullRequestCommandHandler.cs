// ------------------------------------------------------------------------------------------------
// <copyright file="SyncPullRequestCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Application.Features.PullRequests.Commands.SyncPullRequest;

/// <summary>Handler for the <see cref="SyncPullRequestCommand" /> command.</summary>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="pullRequests">The repository of the pull requests.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The forge is the reference: the delivery that asked for the synchronization only named the pull request, and
///     its state, its head and its base are read here. The head is the one of the pull request, never a merge commit.
///   </para>
///   <para>
///     Whatever cannot be recorded leaves nothing to do, so that the job ends instead of being replayed for nothing:
///     a repository that is no longer tracked, a pull request the forge no longer knows, or one whose branch is longer
///     than a branch can be. A failure of the forge, a rate limit or a concurrent save is thrown: the worker replays it.
///   </para>
/// </remarks>
internal sealed class SyncPullRequestCommandHandler(
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  IForgeGatewayProvider gateways,
  IPullRequestRepository pullRequests,
  IClock clock) : ICommandHandler<SyncPullRequestCommand>
{
  #region Implementation of ICommandHandler<SyncPullRequestCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(SyncPullRequestCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repository = await repositories.FindAsync(command.RepositoryId, cancellationToken).ConfigureAwait(false);

    if (repository is not { IsActive: true })
    {
      return Unit.Value;
    }

    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("The scope has not entered an organization.");

    var pull = await gateways.GetGateway(organization.Forge)
                             .GetPullAsync(repository.ToForgeRef(organization.Forge), command.Number, cancellationToken)
                             .ConfigureAwait(false);

    // A branch that does not fit is never cut: the pull request is left out, and its commits are still notified.
    if (pull is null || !PullRequest.CanRecord(pull))
    {
      return Unit.Value;
    }

    await pullRequests.UpsertAsync(repository.Id, pull, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
