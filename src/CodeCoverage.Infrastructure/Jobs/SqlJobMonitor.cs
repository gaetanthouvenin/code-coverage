// ------------------------------------------------------------------------------------------------
// <copyright file="SqlJobMonitor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>Reads the job queue for the administrators, from the <c>Jobs</c> table, the worker heartbeat and the quota meter.</summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="pulse">The heartbeat of the worker loops of this process.</param>
/// <param name="meter">The meter that keeps the last GitHub quota read.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <c>Jobs</c> is an operational table, never filtered by organization: the reads work outside any organization, as
///   <c>IAdminOnly</c> requires. They project metadata only: never the payload, the keys or the error text.
/// </remarks>
internal sealed class SqlJobMonitor(CodeCoverageDbContext context, WorkerPulse pulse, CodeCoverageMeter meter, IClock clock) : IJobMonitor
{
  #region Implementation of IJobMonitor

  /// <inheritdoc />
  public async Task<JobSummary?> FindAsync(long id, CancellationToken cancellationToken) =>
    await Summaries(context.Jobs.AsNoTracking().Where(j => j.Id == id)).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<JobPage> ListAsync(JobState? state, int page, int pageSize, CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

    var jobs = context.Jobs.AsNoTracking();

    if (state is { } wanted)
    {
      jobs = jobs.Where(j => j.State == wanted);
    }

    var total = await jobs.CountAsync(cancellationToken).ConfigureAwait(false);
    var items = await Summaries(jobs.OrderByDescending(static j => j.Id).Skip((page - 1) * pageSize).Take(pageSize))
                      .ToListAsync(cancellationToken)
                      .ConfigureAwait(false);

    return new JobPage(items, page, pageSize, total);
  }

  /// <inheritdoc />
  public async Task<SystemStatus> GetStatusAsync(CancellationToken cancellationToken)
  {
    var now = clock.UtcNow;
    var counts = await context.Jobs
                              .AsNoTracking()
                              .GroupBy(static j => j.State)
                              .Select(static g => new { State = g.Key, Count = g.Count() })
                              .ToListAsync(cancellationToken)
                              .ConfigureAwait(false);
    var oldest = await context.Jobs
                              .AsNoTracking()
                              .Where(j => j.State == JobState.Queued && j.RunAfter <= now)
                              .OrderBy(static j => j.RunAfter)
                              .Select(static j => (DateTimeOffset?)j.RunAfter)
                              .FirstOrDefaultAsync(cancellationToken)
                              .ConfigureAwait(false);

    int Count(JobState state) => counts.FirstOrDefault(c => c.State == state)?.Count ?? 0;

    return new SystemStatus(
      Count(JobState.Queued),
      Count(JobState.Running),
      Count(JobState.Failed),
      Count(JobState.Dead),
      oldest is { } runAfter ? now - runAfter : null,
      pulse.LastBeatAt,
      meter.GitHubRateLimits()
    );
  }

  /// <inheritdoc />
  public async Task<IReadOnlyDictionary<long, int>> CountQueuedByOrganizationAsync(CancellationToken cancellationToken)
  {
    var counts = await context.Jobs
                              .AsNoTracking()
                              .Where(static j => j.OrganizationId != null && (j.State == JobState.Queued || j.State == JobState.Running))
                              .GroupBy(static j => j.OrganizationId!.Value)
                              .Select(static g => new { OrganizationId = g.Key, Count = g.Count() })
                              .ToListAsync(cancellationToken)
                              .ConfigureAwait(false);

    return counts.ToDictionary(static c => c.OrganizationId, static c => c.Count);
  }

  /// <summary>Projects the rows of the queue to their metadata.</summary>
  /// <param name="jobs">The rows.</param>
  /// <returns>The summaries.</returns>
  private static IQueryable<JobSummary> Summaries(IQueryable<JobRecord> jobs) =>
    jobs.Select(static j => new JobSummary
    {
      Id = j.Id,
      OrganizationId = j.OrganizationId,
      Type = j.Type,
      State = j.State,
      Attempts = j.Attempts,
      MaxAttempts = j.MaxAttempts,
      RunAfter = j.RunAfter,
      FirstQueuedAt = j.FirstQueuedAt,
      LockedBy = j.LockedBy,
      LockedUntil = j.LockedUntil,
      HasError = j.LastError != null,
      CorrelationId = j.CorrelationId,
      CreatedAt = j.CreatedAt,
      CompletedAt = j.CompletedAt,
    });

  #endregion
}
