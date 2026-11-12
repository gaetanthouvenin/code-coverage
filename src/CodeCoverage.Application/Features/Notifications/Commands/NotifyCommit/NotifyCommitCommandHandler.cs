// ------------------------------------------------------------------------------------------------
// <copyright file="NotifyCommitCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Notifications.Commands.NotifyCommit;

/// <summary>Handler for the <see cref="NotifyCommitCommand" /> command.</summary>
/// <param name="commits">The repository of the commits.</param>
/// <param name="uploads">The repository of the uploads.</param>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="configResolver">What resolves the configuration of a commit.</param>
/// <param name="notifications">What publishes on the forge.</param>
/// <param name="mergeCommits">What finds the head of a pull request behind a synthetic merge commit.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="notificationOptions">The notification options.</param>
/// <param name="processingOptions">The processing options.</param>
/// <param name="metrics">The measures of the notifications.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The gate is asked again here: between the request and the job, an upload may have arrived. The
///     statuses that did not change are not published again, so running twice publishes nothing twice.
///   </para>
///   <para>
///     The uploads sent with the SHA of a synthetic merge commit are moved to the head of the pull request
///     (ARCHITECTURE §1.4-2), and <c>uploads-complete</c> names the SHA they were sent with. A notification
///     asked by hand on a commit of a pull request that holds no upload is therefore handed to that head, which
///     the forge names: the request could not ask it. The hand-over is the notification of the merge commit, which
///     is marked notified, so that the sweep never hands it over again.
///   </para>
///   <para>
///     A commit notified once, by hand or by the sweep, is notified again whatever <c>manual_trigger</c> and
///     <c>after_n_builds</c> say: the jobs that ask it again, when its base gets its report or its pull request is
///     opened, would otherwise leave its statuses and its comment on an older comparison. It still waits for the
///     uploads in progress. Below <c>after_n_builds</c>, its statuses keep the partial suffix.
///   </para>
///   <para>
///     A commit is marked notified once it is published, or when its notification by the sweep is refused for good: by
///     the forge (a repository, a commit or a status it refuses whatever the number of tries, see
///     <see cref="IForgeGateway.CreateStatusAsync" />) or here (the coverage of the base was compacted, a row is
///     gone). The sweep then never forces it again. A failure a retry can heal (a rate limit, a failure of the forge)
///     leaves it unmarked; if the job dies of it, the next sweep takes the commit again.
///   </para>
/// </remarks>
internal sealed class NotifyCommitCommandHandler(
  ICommitRepository commits,
  IUploadRepository uploads,
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  ConfigResolver configResolver,
  NotificationService notifications,
  MergeCommitDetector mergeCommits,
  IJobQueue jobQueue,
  IOptionsMonitor<NotificationOptions> notificationOptions,
  IOptionsMonitor<ProcessingOptions> processingOptions,
  INotificationMetrics metrics,
  IClock clock) : ICommandHandler<NotifyCommitCommand>
{
  #region Fields

  private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

  #endregion

  #region Methods

  /// <summary>Tells whether a failure is one no retry can heal, as the worker tells it.</summary>
  /// <param name="exception">The failure.</param>
  /// <returns><see langword="true" /> for a refusal the worker fails for good without retrying it.</returns>
  private static bool IsPermanent(Exception exception) =>
    exception is NotFoundException or ValidationException or BadRequestException or UnprocessableEntityException;

  /// <summary>Marks a commit notified, at once.</summary>
  /// <param name="commit">The commit, read in this scope.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the commit is saved.</returns>
  private async Task MarkNotifiedAsync(Commit commit, CancellationToken cancellationToken)
  {
    commit.MarkNotified(clock.UtcNow);
    await commits.UpdateAsync(commit, cancellationToken).ConfigureAwait(false);
  }

  #endregion

  #region Implementation of ICommandHandler<NotifyCommitCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The commit or its repository no longer exists.</exception>
  /// <exception cref="RetryLaterException">
  ///   A forced notification waits for the uploads of the commit or for the finalization that merges them, or the base
  ///   is still being processed.
  /// </exception>
  public async ValueTask<Unit> Handle(NotifyCommitCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var commit = await commits.FindAsync(command.CommitId, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfNotifiedTargetGone(commit);

    var repository = await repositories.FindAsync(commit.RepositoryId, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfNotifiedTargetGone(repository);

    // A repository whose coverage is no longer processed (left, or disabled by an administrator) receives nothing.
    if (!repository.IsTracked)
    {
      return Unit.Value;
    }

    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfNotifiedTargetGone(organization);
    var address = repository.ToForgeRef(organization.Forge);
    var all = await uploads.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false);

    // Asked by hand on the SHA of a merge commit, whose uploads were moved to the head of the pull request: the head
    // is notified in its place. The merge commit holds no upload then, or only failed ones: a copy sent again after
    // the move is refused as a duplicate of the head, and stays here.
    if (command.Force && commit.PullNumber is { } pullNumber && all.All(static upload => upload.State is UploadState.Failed))
    {
      if (await mergeCommits.DetectAsync(repository.Id, address, commit.Sha, pullNumber, cancellationToken).ConfigureAwait(false) is { } headSha)
      {
        if (await commits.FindByShaAsync(repository.Id, headSha, cancellationToken).ConfigureAwait(false) is { } head)
        {
          // Asked by the sweep, the head is notified as the sweep would: with the partial suffix.
          var handOver = command.Partial ? NotifyCommitJobs.CreateForSweep(head.Id) : NotifyCommitJobs.CreateForced(head.Id);

          await jobQueue.EnqueueAsync(handOver, cancellationToken).ConfigureAwait(false);
        }

        await MarkNotifiedAsync(commit, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
      }

      // The commit is the head itself: without any upload it has nothing to notify; its failed uploads are its own.
      if (all.Count == 0)
      {
        await MarkNotifiedAsync(commit, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
      }
    }

    var resolved = await configResolver.ResolveAsync(repository, address, commit.Sha, cancellationToken).ConfigureAwait(false);
    var inProgress = all.Count(static upload => upload.State is UploadState.Received or UploadState.Processing);
    var processed = all.Count(static upload => upload.State is UploadState.Processed);
    var now = clock.UtcNow;

    var decision = NotificationGate.Decide(
      commit,
      inProgress,
      processed,
      resolved.Config.Notify,
      notificationOptions.CurrentValue.AfterNBuildsCountsCarryforward,
      command.Force
    );

    // Notified once already: what held the first notification back no longer holds the next ones.
    var again = commit.NotifiedAt is not null && decision is NotificationDecision.ManualTriggerExpected or NotificationDecision.BuildsExpected;

    if (decision is not NotificationDecision.Notify && !again)
    {
      // Asked by hand while uploads are in progress, or before the finalization that merges them ran, the first one
      // as the next ones: it waits for them, as long as the commit is not stuck. Any other wait ends with the
      // finalization, which queues its own notification.
      var waitedSince = commit.LastUploadAt ?? commit.UpdatedAt;

      if (command.Force
          && decision is NotificationDecision.UploadsInProgress or NotificationDecision.NotFinalized
          && now - waitedSince < TimeSpan.FromMinutes(processingOptions.CurrentValue.PendingTimeoutMinutes))
      {
        throw new RetryLaterException(RetryDelay, $"The uploads of commit {commit.Sha.Value} are still being processed.");
      }

      return Unit.Value;
    }

    int published;

    try
    {
      published = await notifications.NotifyAsync(
                    repository,
                    organization.Forge,
                    commit,
                    resolved.Config,
                    all.Count(static upload => upload.State is UploadState.Failed),
                    resolved.Errors.Count > 0,
                    command.Partial || (again && decision is NotificationDecision.BuildsExpected)
                      ? NotificationGate.PartialSuffix(commit, resolved.Config.Notify, notificationOptions.CurrentValue.AfterNBuildsCountsCarryforward)
                      : null,
                    cancellationToken
                  ).ConfigureAwait(false);
    }
    catch (Exception exception) when (command.Partial && commit.NotifiedAt is null && IsPermanent(exception))
    {
      // Refused for good, by the forge or here: the sweep would force it again at each round, for nothing.
      await MarkNotifiedAsync(commit, cancellationToken).ConfigureAwait(false);

      throw;
    }

    if (published > 0 && commit.LastUploadAt is { } lastUpload)
    {
      metrics.NotifyLatency(clock.UtcNow - lastUpload);
    }

    await MarkNotifiedAsync(commit, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}