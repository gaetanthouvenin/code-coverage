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
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Ports;

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
/// <param name="notifications">The notification options.</param>
/// <param name="clock">The clock.</param>
/// <remarks>The job is serialized by its lock key. The rebuild is complete and deterministic, whatever the order of arrival.</remarks>
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
  IOptionsMonitor<NotificationOptions> notifications,
  IClock clock) : ICommandHandler<FinalizeCommitCommand>
{
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

    // The finalization just merged every upload that was processed.
    if (NotificationGate.Decide(commit, 0, commit.UploadedSessionCount, config.Notify, options.AfterNBuildsCountsCarryforward, forced: false)
        is not NotificationDecision.Notify)
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

    // Before the save: the notification it queues builds the flag statuses from the flags known to the repository.
    await repositoryFlags.AddMissingAsync(
      commit.RepositoryId,
      [.. sessions.SelectMany(static session => session.Session.Flags).Distinct(StringComparer.Ordinal)],
      clock.UtcNow,
      cancellationToken
    ).ConfigureAwait(false);

    commit.RecordFinalization(
      processed.Length == 0 ? CommitState.Error : CommitState.Complete,
      merged.Files.Count,
      merged.Totals,
      sessions.Length,
      processed.Length,
      carried.Status,
      resolved.Source.ToString(),
      clock.UtcNow
    );

    await reports.SaveFinalizedAsync(
      commit,
      codec.Encode(merged),
      codec.EncodeFileIndex(merged),
      [.. sessions.Select(static session => session.Session)],
      merger.BuildFlagTotals(commit, resolved.Config, sessions, merged),
      WakeUps(commit, wasComplete, carried) with { Notification = Notification(commit, resolved.Config) },
      cancellationToken
    ).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
