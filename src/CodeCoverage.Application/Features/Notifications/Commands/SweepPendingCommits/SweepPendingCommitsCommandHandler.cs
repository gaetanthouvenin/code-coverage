// ------------------------------------------------------------------------------------------------
// <copyright file="SweepPendingCommitsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Notifications.Commands.SweepPendingCommits;

/// <summary>Handler for the <see cref="SweepPendingCommitsCommand" /> command.</summary>
/// <param name="currentOrganization">The organization of the scope.</param>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="commits">The repository of the commits.</param>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <param name="configResolver">What resolves the configuration of a commit.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="processing">The settings of the processing, read at each use.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     A commit is stuck when it waits for uploads that never come: <c>codecov.notify.after_n_builds</c> is not
///     reached, and nothing will ever notify it. Past <c>Processing:PendingTimeoutMinutes</c> after its last upload
///     it is notified by force, and its statuses say how many uploads it received. No pending status is ever
///     published in the meantime.
///   </para>
///   <para>
///     Left out: a commit of a repository that notifies on demand (<c>codecov.notify.manual_trigger</c>), a commit
///     with an upload still received or being processed, a commit that is not finalized, a repository that is no
///     longer tracked, and a commit whose last upload is older than <see cref="Horizon" />.
///   </para>
/// </remarks>
internal sealed class SweepPendingCommitsCommandHandler(
  ICurrentOrganization currentOrganization,
  IOrganizationDirectory directory,
  ICommitRepository commits,
  IRepositoryRepository repositories,
  ConfigResolver configResolver,
  IJobQueue jobQueue,
  IOptionsMonitor<ProcessingOptions> processing,
  IClock clock) : ICommandHandler<SweepPendingCommitsCommand>
{
  #region Fields

  /// <summary>
  ///   The number of jobs one sweep of an organization queues, at most, and the size of a page it reads; the next sweep
  ///   takes the following ones. The commits left out are read past, and so are those whose notification is already
  ///   queued (waiting for its retry, for instance): neither takes the place of those after them.
  /// </summary>
  internal const int MaxCommits = 200;

  /// <summary>
  ///   The number of pages one sweep reads, at most. The next sweep starts again from the oldest commit: past this many
  ///   commits left out in a row, the later ones wait until the older ones are notified or leave the
  ///   <see cref="Horizon" />.
  /// </summary>
  internal const int MaxPages = 10;

  /// <summary>
  ///   How far back the sweep looks. A commit that was left out of every sweep for that long, because its repository
  ///   notifies on demand for instance, is abandoned: without this bound such commits would fill every sweep.
  /// </summary>
  internal static readonly TimeSpan Horizon = TimeSpan.FromDays(7);

  #endregion

  #region Methods

  /// <summary>Queues the sweep of each active organization: the coordinator reads no commit itself (ADR-013).</summary>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the sweeps are queued.</returns>
  private async Task CoordinateAsync(CancellationToken cancellationToken)
  {
    foreach (var organization in await directory.ListActiveAsync(cancellationToken).ConfigureAwait(false))
    {
      var job = JobRequest.ForOrganization(
        organization.Id,
        JobTypes.SweepPendingCommits,
        "{}",
        SweepPendingCommitsJobs.Key,
        SweepPendingCommitsJobs.Key,
        coalesce: true
      );

      await jobQueue.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Queues the forced notification of the stuck commits of the organization of the scope.</summary>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the notifications are queued.</returns>
  private async Task SweepAsync(CancellationToken cancellationToken)
  {
    var now = clock.UtcNow;
    var timeout = TimeSpan.FromMinutes(Math.Max(1, processing.CurrentValue.PendingTimeoutMinutes));
    var tracked = new Dictionary<long, TrackedRepository?>();
    Organization? organization = null;
    Commit? after = null;
    var queued = 0;

    for (var page = 1; page <= MaxPages; page++)
    {
      var stuck = await commits.ListStuckAsync(now - Horizon, now - timeout, after, MaxCommits, cancellationToken).ConfigureAwait(false);

      foreach (var commit in stuck)
      {
        organization ??= await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException("The scope has not entered an organization.");

        // A request merged into the notification already queued for the commit queues nothing: it is not counted.
        if (await IsSweptAsync(commit, organization, tracked, cancellationToken).ConfigureAwait(false)
            && await jobQueue.TryEnqueueAsync(NotifyCommitJobs.CreateForSweep(commit.Id), cancellationToken).ConfigureAwait(false)
            && ++queued == MaxCommits)
        {
          return;
        }
      }

      if (stuck.Count < MaxCommits)
      {
        return;
      }

      after = stuck[^1];
    }
  }

  /// <summary>Tells whether a stuck commit is notified by force.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="organization">The organization of the scope.</param>
  /// <param name="tracked">The repositories already read, by identifier.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   <see langword="false" /> for a commit of a repository that is no longer tracked, or that notifies on demand.
  /// </returns>
  private async Task<bool> IsSweptAsync(Commit commit, Organization organization, Dictionary<long, TrackedRepository?> tracked, CancellationToken cancellationToken)
  {
    if (!tracked.TryGetValue(commit.RepositoryId, out var repository))
    {
      repository = await repositories.FindAsync(commit.RepositoryId, cancellationToken).ConfigureAwait(false);
      tracked[commit.RepositoryId] = repository;
    }

    if (repository is not { IsTracked: true })
    {
      return false;
    }

    // The configuration of a finalized commit is already resolved and kept: no call to the forge.
    var resolved = await configResolver.ResolveAsync(repository, repository.ToForgeRef(organization.Forge), commit.Sha, cancellationToken)
                                       .ConfigureAwait(false);

    // Notified when its pipeline says so, never by a timer.
    return !resolved.Config.Notify.ManualTrigger;
  }

  #endregion

  #region Implementation of ICommandHandler<SweepPendingCommitsCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(SweepPendingCommitsCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentOrganization.IsKnown)
    {
      await SweepAsync(cancellationToken).ConfigureAwait(false);
    }
    else
    {
      await CoordinateAsync(cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
