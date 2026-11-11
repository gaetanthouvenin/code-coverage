// ------------------------------------------------------------------------------------------------
// <copyright file="FinalizeCommitCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Commits.Commands.FinalizeCommit;

/// <summary>Handler for the <see cref="FinalizeCommitCommand" /> command.</summary>
/// <param name="commits">The commits of the organization.</param>
/// <param name="uploads">The uploads of the organization.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="configResolver">The effective configuration of a commit.</param>
/// <param name="reports">The blobs and the saving of the finalization.</param>
/// <param name="codec">The encoding of the coverage.</param>
/// <param name="merger">Rebuilds the report from the sessions.</param>
/// <param name="carryforward">Finds what the commit inherits from its ancestors.</param>
/// <param name="repositoryFlags">The flags the repositories received coverage for.</param>
/// <param name="branches">The repository of the branches.</param>
/// <param name="tracker">What finds the covered head of a branch.</param>
/// <param name="pullRequests">The repository of the pull requests.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="notifications">The notification options.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>The job is serialized by its lock key. The rebuild is complete and deterministic, whatever the order of arrival.</para>
///   <para>
///     A branch without a row gets one only when the forge has it, at the head the forge gives: a late finalization
///     never brings back a branch deleted since. The commit of a pull request creates none, since its branch may be a
///     fork's, which the repository does not have.
///   </para>
/// </remarks>
internal sealed class FinalizeCommitCommandHandler(
  ICommitRepository commits,
  IUploadRepository uploads,
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  ConfigResolver configResolver,
  ICommitReportRepository reports,
  ICoverageReportCodec codec,
  CommitMerger merger,
  CarryforwardResolver carryforward,
  IRepositoryFlagRepository repositoryFlags,
  IBranchRepository branches,
  BranchHeadTracker tracker,
  IPullRequestRepository pullRequests,
  IForgeGatewayProvider gateways,
  IOptionsMonitor<NotificationOptions> notifications,
  IClock clock) : ICommandHandler<FinalizeCommitCommand>
{
  #region Fields

  /// <summary>
  ///   The number of pull requests one finalization notifies again, at most: those most recently updated. The others
  ///   keep the comparison they were notified with until their head is notified again, by its next upload, a push to
  ///   the pull request or a request by hand.
  /// </summary>
  internal const int MaxDependentNotifications = 50;

  #endregion

  #region Methods

  /// <summary>Builds the refusal of a commit that cannot be read.</summary>
  /// <returns>The exception.</returns>
  private static NotFoundException NotFound() => new(CommitErrors.NotFound, "The commit no longer exists.");

  /// <summary>Gives the finalizations this one wakes, queued in its transaction (ARCHITECTURE §6.5, step 4.3).</summary>
  /// <param name="commit">The commit, carrying its finalization.</param>
  /// <param name="wasComplete">Whether the commit was complete before this finalization.</param>
  /// <param name="carried">What it inherited.</param>
  /// <returns>
  ///   The wake-ups: a commit that becomes complete wakes the waiting commits, but one that was already complete wakes
  ///   none, or two waiting commits would requeue each other without end; a commit left waiting looks again at the
  ///   pending ancestors it passed.
  /// </returns>
  private static FinalizationWakeUps WakeUps(Commit commit, bool wasComplete, CarryforwardResolution carried) =>
    new(
      !wasComplete && commit.State is CommitState.Complete,
      carried.Status is CarryforwardStatus.AncestorPending ? carried.PendingAncestorIds : []
    );

  /// <summary>Builds the notification of a commit that was just finalized, when the gate lets it through (ARCHITECTURE §7.6).</summary>
  /// <param name="commit">The commit, carrying its finalization: no upload is in progress.</param>
  /// <param name="config">The effective configuration of the commit.</param>
  /// <returns>The debounced job, queued in the transaction of the finalization, or <see langword="null" />.</returns>
  private JobRequest? Notification(Commit commit, CoverageConfig config)
  {
    var options = notifications.CurrentValue;

    // The finalization just merged every upload that was processed. A commit notified once already, as a part of
    // its uploads by the sweep, or by its pipeline under manual_trigger, is notified again: its statuses must leave the
    // former report, and a notification asked while these uploads were in progress was dropped for this one to take
    // over. Under manual_trigger, a commit never notified waits for its pipeline, as in Codecov.
    var decision = NotificationGate.Decide(commit, 0, commit.UploadedSessionCount, config.Notify, options.AfterNBuildsCountsCarryforward, forced: false);
    var again = commit.NotifiedAt is not null && decision is NotificationDecision.ManualTriggerExpected or NotificationDecision.BuildsExpected;

    if (decision is not NotificationDecision.Notify && !again)
    {
      return null;
    }

    return NotifyCommitJobs.Create(
      commit.Id,
      clock.UtcNow,
      TimeSpan.FromSeconds(Math.Max(0, options.DebounceSeconds)),
      TimeSpan.FromSeconds(Math.Max(1, options.MaxDelaySeconds))
    );
  }

  /// <summary>Builds the notifications of the pull requests whose base is the commit that completes.</summary>
  /// <param name="commit">The commit, which becomes complete.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>One notification per head commit to notify again; empty when no pull request waited for this report.</returns>
  /// <remarks>
  ///   The base was processed after the pull request: the pull requests that forked from this commit and were compared
  ///   to something else, an older ancestor or nothing, now have a better base. Their head is notified again when its
  ///   own report is ready, with the debounced job; a head that was never notified still waits for
  ///   <c>after_n_builds</c> and <c>manual_trigger</c>. Once a head is compared to this commit it is no longer listed,
  ///   which ends the round trips. At most <see cref="MaxDependentNotifications" /> are notified.
  /// </remarks>
  private async Task<IReadOnlyList<JobRequest>> NotifyDependentsAsync(Commit commit, CancellationToken cancellationToken)
  {
    var open = await pullRequests.ListOpenByForkPointAsync(commit.RepositoryId, commit.Sha, MaxDependentNotifications, cancellationToken).ConfigureAwait(false);
    GitSha[] heads =
    [
      .. open.Where(pull => pull.ComparedToSha != commit.Sha && pull.HeadSha != commit.Sha)
             .Select(static pull => pull.HeadSha)
             .Distinct()
             .Take(MaxDependentNotifications),
    ];

    if (heads.Length == 0)
    {
      return [];
    }

    var options = notifications.CurrentValue;
    var debounce = TimeSpan.FromSeconds(Math.Max(0, options.DebounceSeconds));
    var maxDelay = TimeSpan.FromSeconds(Math.Max(1, options.MaxDelaySeconds));
    var found = await commits.ListByShasAsync(commit.RepositoryId, heads, cancellationToken).ConfigureAwait(false);

    return
    [
      .. found.Where(static head => head.State is CommitState.Complete or CommitState.Error)
              .OrderBy(static head => head.Id)
              .Select(head => NotifyCommitJobs.Create(head.Id, clock.UtcNow, debounce, maxDelay)),
    ];
  }

  /// <summary>Reads the sessions of the uploads that were processed.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="processed">The uploads, by session number.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The sessions and their coverage.</returns>
  private async Task<IReadOnlyList<SessionCoverage>> ReadSessionsAsync(Commit commit, IReadOnlyList<Upload> processed, CancellationToken cancellationToken)
  {
    var sessions = new List<SessionCoverage>(processed.Count);

    foreach (var upload in processed)
    {
      var blob = await reports.GetUploadCoverageAsync(upload.Id, cancellationToken).ConfigureAwait(false)
                 ?? throw new UnprocessableEntityException(CommitErrors.Compacted, "The coverage of an upload of this commit no longer exists.");
      var report = codec.Decode(blob);
      var session = Session.FromUpload(commit.Id, upload.Sequence, upload.Id, upload.Flags, upload.Name, upload.CiService, upload.BuildUrl, report.Totals);

      sessions.Add(new SessionCoverage(session, report));
    }

    return sessions;
  }

  #endregion

  #region Implementation of ICommandHandler<FinalizeCommitCommand>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The commit no longer exists.</exception>
  /// <exception cref="UnprocessableEntityException">The coverage of a processed upload no longer exists.</exception>
  /// <exception cref="RetryLaterException">A closer ancestor is pending and the wait is not over.</exception>
  public async ValueTask<Unit> Handle(FinalizeCommitCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var commit = await commits.FindAsync(command.CommitId, cancellationToken).ConfigureAwait(false) ?? throw NotFound();
    var all = await uploads.ListByCommitAsync(commit.Id, cancellationToken).ConfigureAwait(false);

    // The last upload to end queues the next finalization.
    if (all.Count == 0 || all.Any(static upload => upload.State is UploadState.Received or UploadState.Processing))
    {
      return Unit.Value;
    }

    var repository = await repositories.FindAsync(commit.RepositoryId, cancellationToken).ConfigureAwait(false) ?? throw NotFound();
    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false) ?? throw NotFound();
    var address = repository.ToForgeRef(organization.Forge);
    var resolved = await configResolver.ResolveAsync(repository, address, commit.Sha, cancellationToken).ConfigureAwait(false);
    Upload[] processed = [.. all.Where(static upload => upload.State is UploadState.Processed).OrderBy(static upload => upload.Sequence)];
    var uploaded = await ReadSessionsAsync(commit, processed, cancellationToken).ConfigureAwait(false);
    var uploadedFlags = uploaded.SelectMany(static session => session.Session.Flags).ToHashSet(StringComparer.Ordinal);
    var carried = await carryforward.ResolveAsync(commit, address, resolved.Config, uploadedFlags, cancellationToken).ConfigureAwait(false);
    SessionCoverage[] sessions = [.. uploaded, .. carried.Sessions];
    var merged = merger.MergeProject(resolved.Config, sessions);
    var wasComplete = commit.State is CommitState.Complete;
    var outcome = processed.Length == 0 ? CommitState.Error : CommitState.Complete;

    // Read before the finalization is recorded on the commit: the ancestry may save what it fetches from the forge,
    // and a save of the scope must not write the commit before SaveFinalizedAsync does.
    var branch = commit.Branch is null ? null : await branches.FindAsync(commit.RepositoryId, commit.Branch, cancellationToken).ConfigureAwait(false);
    var head = branch?.HeadSha;

    if (commit.Branch is not null && branch is null && commit.PullNumber is null)
    {
      head = await gateways.GetGateway(address.Forge).GetBranchHeadAsync(address, commit.Branch, cancellationToken).ConfigureAwait(false);
    }

    var covered = head is null
                    ? null
                    : await tracker.TrackAsync(commit.RepositoryId, address, head, commit, outcome, cancellationToken).ConfigureAwait(false);

    // Before the save: the notification it queues builds the flag statuses from the flags known to the repository.
    await repositoryFlags.AddMissingAsync(
      commit.RepositoryId,
      [.. sessions.SelectMany(static session => session.Session.Flags).Distinct(StringComparer.Ordinal)],
      clock.UtcNow,
      cancellationToken
    ).ConfigureAwait(false);

    // Read before the finalization is recorded, as the branch: nothing is saved between it and SaveFinalizedAsync.
    var dependents = outcome is CommitState.Complete ? await NotifyDependentsAsync(commit, cancellationToken).ConfigureAwait(false) : [];

    commit.RecordFinalization(
      outcome,
      merged.Files.Count,
      merged.Totals,
      sessions.Length,
      processed.Length,
      carried.Status,
      resolved.Source.ToString(),
      clock.UtcNow
    );

    if (commit.Branch is not null && head is not null)
    {
      // No push told about this branch yet: its row starts at the head the forge gives, and the next push moves it.
      branch ??= Branch.Create(commit.RepositoryId, commit.Branch, head, clock.UtcNow);
      branch.Cover(covered, clock.UtcNow);
    }

    await reports.SaveFinalizedAsync(
      commit,
      codec.Encode(merged),
      codec.EncodeFileIndex(merged),
      [.. sessions.Select(static session => session.Session)],
      merger.BuildFlagTotals(commit, resolved.Config, sessions, merged),
      branch,
      WakeUps(commit, wasComplete, carried) with { Notification = Notification(commit, resolved.Config), DependentNotifications = dependents },
      cancellationToken
    ).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
