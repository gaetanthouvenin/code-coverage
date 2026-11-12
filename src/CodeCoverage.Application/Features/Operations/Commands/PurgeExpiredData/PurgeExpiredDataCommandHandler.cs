// ------------------------------------------------------------------------------------------------
// <copyright file="PurgeExpiredDataCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Operations.Commands.PurgeExpiredData;

/// <summary>Handler for the <see cref="PurgeExpiredDataCommand" /> command.</summary>
/// <param name="currentOrganization">The organization of the lane of the job, if any.</param>
/// <param name="directory">The directory of the organizations, read by the coordinator.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="retention">Deletes what the retention no longer keeps.</param>
/// <param name="options">The retention settings of the instance, read at each run.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     On the lane of the instance, it is the coordinator (ADR-013): it purges the operational tables (<c>Jobs</c> after
///     <c>Retention:JobDays</c>, <c>WebhookDeliveries</c> after <c>Retention:WebhookDays</c>), reads <c>Organizations</c>
///     only, and queues one purge per active organization.
///   </para>
///   <para>
///     In an organization, it deletes the raw files of the uploads whose time is over (<c>Retention:RawDays</c>, set when
///     they are written), compacts the commits off the default branch after <c>Retention:BranchReportDays</c>, and drops
///     the diffs and trees read from the forge after <c>Retention:DiffCacheDays</c>. The comparisons are kept.
///   </para>
/// </remarks>
internal sealed partial class PurgeExpiredDataCommandHandler(
  ICurrentOrganization currentOrganization,
  IOrganizationDirectory directory,
  IJobQueue jobQueue,
  IRetentionRepository retention,
  IOptionsMonitor<RetentionOptions> options,
  IClock clock,
  ILogger<PurgeExpiredDataCommandHandler> logger) : ICommandHandler<PurgeExpiredDataCommand>
{
  #region Methods

  /// <summary>Purges the operational tables and queues the purge of each active organization.</summary>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the purges are queued.</returns>
  private async Task CoordinateAsync(CancellationToken cancellationToken)
  {
    var settings = options.CurrentValue;
    var now = clock.UtcNow;
    var jobs = await retention.PurgeJobsAsync(now - Days(settings.JobDays), PurgeExpiredDataJobs.BatchSize, cancellationToken).ConfigureAwait(false);
    var deliveries = await retention.PurgeWebhookDeliveriesAsync(now - Days(settings.WebhookDays), PurgeExpiredDataJobs.BatchSize, cancellationToken)
                                    .ConfigureAwait(false);

    LogInstancePurged(logger, jobs, deliveries);

    foreach (var organization in await directory.ListActiveAsync(cancellationToken).ConfigureAwait(false))
    {
      var job = JobRequest.ForOrganization(organization.Id, JobTypes.PurgeExpiredData, "{}", PurgeExpiredDataJobs.Key, PurgeExpiredDataJobs.Key, coalesce: true);

      await jobQueue.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Purges the tables of the organization of the scope.</summary>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>A task that completes when the purge is done.</returns>
  private async Task PurgeAsync(CancellationToken cancellationToken)
  {
    var settings = options.CurrentValue;
    var now = clock.UtcNow;
    var raw = await retention.PurgeRawUploadsAsync(now, PurgeExpiredDataJobs.BatchSize, cancellationToken).ConfigureAwait(false);
    var compacted = await retention.CompactCommitsAsync(now - Days(settings.BranchReportDays), PurgeExpiredDataJobs.BatchSize, cancellationToken)
                                   .ConfigureAwait(false);
    var caches = await retention.PurgeForgeCachesAsync(now - Days(settings.DiffCacheDays), PurgeExpiredDataJobs.BatchSize, cancellationToken)
                                .ConfigureAwait(false);

    LogOrganizationPurged(logger, currentOrganization.Id, raw, compacted, caches);
  }

  /// <summary>Turns a number of days into a duration, one day at least whatever the setting.</summary>
  /// <param name="days">The number of days.</param>
  /// <returns>The duration.</returns>
  private static TimeSpan Days(int days) => TimeSpan.FromDays(Math.Max(1, days));

  [LoggerMessage(EventId = 1700, Level = LogLevel.Information, Message = "Purge of the instance: {Jobs} jobs and {Deliveries} webhook deliveries deleted")]
  private static partial void LogInstancePurged(ILogger logger, int jobs, int deliveries);

  [LoggerMessage(
    EventId = 1701,
    Level = LogLevel.Information,
    Message = "Purge of organization {OrganizationId}: {RawFiles} raw files deleted, {Commits} commits compacted, {Caches} cached diffs and trees deleted"
  )]
  private static partial void LogOrganizationPurged(ILogger logger, long organizationId, int rawFiles, int commits, int caches);

  #endregion

  #region Implementation of ICommandHandler<PurgeExpiredDataCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(PurgeExpiredDataCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentOrganization.IsKnown)
    {
      await PurgeAsync(cancellationToken).ConfigureAwait(false);
    }
    else
    {
      await CoordinateAsync(cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
