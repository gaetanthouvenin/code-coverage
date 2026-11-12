// ------------------------------------------------------------------------------------------------
// <copyright file="SyncPullRequestCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Application.Features.PullRequests.Commands.SyncPullRequest;

/// <summary>Handler for the <see cref="SyncPullRequestCommand" /> command.</summary>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="pullRequests">The repository of the pull requests.</param>
/// <param name="commits">The repository of the commits.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="notifications">The settings of the notifications, read at each use.</param>
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
  ICommitRepository commits,
  IJobQueue jobQueue,
  IOptionsMonitor<NotificationOptions> notifications,
  IClock clock) : ICommandHandler<SyncPullRequestCommand>
{
  #region Methods

  /// <summary>Queues the notification of the head of an open pull request, when its report is ready.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="headSha">The head of the pull request.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the notification is queued, or when there is none to queue.</returns>
  /// <remarks>
  ///   The upload may have come before the pull request was opened: the commit was then notified as a push, and
  ///   its comment appears now. A head whose report is not complete yet is notified by its own finalization. The
  ///   job is the debounced one, not a forced one: <c>after_n_builds</c> and <c>manual_trigger</c> still apply.
  /// </remarks>
  private async Task NotifyHeadAsync(long repositoryId, GitSha headSha, CancellationToken cancellationToken)
  {
    var found = await commits.ListByShasAsync(repositoryId, [headSha], cancellationToken).ConfigureAwait(false);

    if (found is not [{ State: CommitState.Complete } head])
    {
      return;
    }

    var options = notifications.CurrentValue;
    var job = NotifyCommitJobs.Create(
      head.Id,
      clock.UtcNow,
      TimeSpan.FromSeconds(Math.Max(0, options.DebounceSeconds)),
      TimeSpan.FromSeconds(Math.Max(1, options.MaxDelaySeconds))
    );

    // After the pull request is saved: a notification that ran before it would take the commit for a push.
    await jobQueue.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);
  }

  #endregion

  #region Implementation of ICommandHandler<SyncPullRequestCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(SyncPullRequestCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repository = await repositories.FindAsync(command.RepositoryId, cancellationToken).ConfigureAwait(false);

    if (repository is not { IsTracked: true })
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

    var recorded = await pullRequests.UpsertAsync(repository.Id, pull, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    if (recorded.AcceptsComments)
    {
      await NotifyHeadAsync(repository.Id, recorded.HeadSha, cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
