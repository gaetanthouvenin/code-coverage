// ------------------------------------------------------------------------------------------------
// <copyright file="OperationalStatusPublisher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   Writes the heartbeat of the worker of this process and the changed GitHub quotas every 15 seconds, so that a process
///   without worker (the Web) reads them; removes the heartbeats older than 24 hours. Hosted by the Api only.
/// </summary>
/// <param name="scopes">Creates the scope of each pass.</param>
/// <param name="pulse">The beat of the worker of this process.</param>
/// <param name="quotas">The quotas reported by the answers of GitHub.</param>
/// <param name="clock">The clock.</param>
/// <param name="timeProvider">The time of the delays.</param>
/// <param name="logger">The logger.</param>
internal sealed partial class OperationalStatusPublisher(
  IServiceScopeFactory scopes,
  WorkerPulse pulse,
  GitHubQuotaTracker quotas,
  IClock clock,
  TimeProvider timeProvider,
  ILogger<OperationalStatusPublisher> logger) : BackgroundService
{
  #region Fields

  /// <summary>The time between two passes.</summary>
  public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

  /// <summary>How long the heartbeat of a stopped worker is kept.</summary>
  public static readonly TimeSpan HeartbeatRetention = TimeSpan.FromHours(24);

  private readonly DateTimeOffset _startedAt = clock.UtcNow;

  private long _version;

  #endregion

  #region Properties

  /// <summary>Gets the name of this worker: its machine name and the identifier of its process.</summary>
  public static string Host { get; } = string.Create(CultureInfo.InvariantCulture, $"{Environment.MachineName}:{Environment.ProcessId}");

  #endregion

  #region Methods

  /// <summary>Writes the heartbeat and the quotas changed since the previous pass, and removes the old heartbeats.</summary>
  /// <param name="cancellationToken">A token to cancel the pass.</param>
  /// <returns>A task that completes when the pass is saved.</returns>
  /// <remarks>An update first, an insertion when no row matched: no SQL of one engine.</remarks>
  public async Task PublishAsync(CancellationToken cancellationToken)
  {
    var scope = scopes.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      var context = scope.ServiceProvider.GetRequiredService<CodeCoverageDbContext>();
      var now = clock.UtcNow;

      if (pulse.LastBeatAt is { } beat)
      {
        await UpsertHeartbeatAsync(context, WorkerHeartbeat.Create(Host, _startedAt, beat), cancellationToken).ConfigureAwait(false);
      }

      var (changed, version) = quotas.ChangedSince(_version);

      foreach (var quota in changed)
      {
        await UpsertQuotaAsync(context, quota, cancellationToken).ConfigureAwait(false);
      }

      _version = version;

      var expired = now - HeartbeatRetention;
      await context.WorkerHeartbeats.Where(h => h.LastBeatAt < expired).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(Interval, timeProvider);

    do
    {
      try
      {
        await PublishAsync(stoppingToken).ConfigureAwait(false);
      }
      catch (Exception exception) when (exception is not OperationCanceledException)
      {
        // The status is best effort: a failed pass is logged, the next one writes it again.
        LogPublishFailed(logger, exception);
      }
    }
    while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
  }

  [LoggerMessage(EventId = 2601, Level = LogLevel.Warning, Message = "The operational status could not be written")]
  private static partial void LogPublishFailed(ILogger logger, Exception exception);

  private static async Task UpsertHeartbeatAsync(CodeCoverageDbContext context, WorkerHeartbeat heartbeat, CancellationToken cancellationToken)
  {
    var updated = await context.WorkerHeartbeats
                               .Where(h => h.Host == heartbeat.Host)
                               .ExecuteUpdateAsync(s => s.SetProperty(static h => h.LastBeatAt, heartbeat.LastBeatAt).SetProperty(static h => h.StartedAt, heartbeat.StartedAt), cancellationToken)
                               .ConfigureAwait(false);

    if (updated == 0)
    {
      await InsertAsync(context, heartbeat, WorkerHeartbeatConfiguration.PrimaryKeyName, cancellationToken).ConfigureAwait(false);
    }
  }

  // The row keeps the newest observation: an older one, from another replica or a late pass, changes nothing, and its
  // insertion then fails on the existing row, which stands.
  private static async Task UpsertQuotaAsync(CodeCoverageDbContext context, GitHubQuota quota, CancellationToken cancellationToken)
  {
    var updated = await context.GitHubQuotas
                               .Where(q => q.Installation == quota.Installation && q.ObservedAt < quota.ObservedAt)
                               .ExecuteUpdateAsync(
                                 s => s.SetProperty(static q => q.Remaining, quota.Remaining)
                                       .SetProperty(static q => q.Limit, quota.Limit)
                                       .SetProperty(static q => q.ResetAt, quota.ResetAt)
                                       .SetProperty(static q => q.ObservedAt, quota.ObservedAt),
                                 cancellationToken)
                               .ConfigureAwait(false);

    if (updated == 0)
    {
      await InsertAsync(context, quota, GitHubQuotaConfiguration.PrimaryKeyName, cancellationToken).ConfigureAwait(false);
    }
  }

  // Only this process writes its own heartbeat; two processes reporting the same installation race only on the first
  // insertion, which the next pass corrects.
  private static async Task InsertAsync<T>(CodeCoverageDbContext context, T row, string primaryKeyName, CancellationToken cancellationToken)
    where T : class
  {
    context.Add(row);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, primaryKeyName))
    {
      // Another process inserted the row in the meantime: its value stands until the next pass.
    }
    finally
    {
      context.Entry(row).State = EntityState.Detached;
    }
  }

  #endregion
}
