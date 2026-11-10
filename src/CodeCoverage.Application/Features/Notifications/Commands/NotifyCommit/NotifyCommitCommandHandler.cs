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
///     the forge names: the request could not ask it.
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

  /// <summary>Builds the refusal of a commit that can no longer be notified.</summary>
  /// <returns>The exception.</returns>
  private static NotFoundException NotFound() => new(NotificationErrors.CommitNotFound, "The commit to notify no longer exists.");

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

    var commit = await commits.FindAsync(command.CommitId, cancellationToken).ConfigureAwait(false) ?? throw NotFound();
    var repository = await repositories.FindAsync(commit.RepositoryId, cancellationToken).ConfigureAwait(false) ?? throw NotFound();

    // A repository that is no longer tracked receives nothing.
    if (!repository.IsActive)
    {
      return Unit.Value;
    }

    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false) ?? throw NotFound();
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
          await jobQueue.EnqueueAsync(NotifyCommitJobs.CreateForced(head.Id), cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
      }

      // The commit is the head itself: without any upload it has nothing to notify; its failed uploads are its own.
      if (all.Count == 0)
      {
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

    if (decision is not NotificationDecision.Notify)
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

    var published = await notifications.PublishStatusesAsync(repository, organization.Forge, commit, resolved.Config, cancellationToken).ConfigureAwait(false);

    if (published > 0 && commit.LastUploadAt is { } lastUpload)
    {
      metrics.NotifyLatency(clock.UtcNow - lastUpload);
    }

    commit.MarkNotified(clock.UtcNow);
    await commits.UpdateAsync(commit, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}