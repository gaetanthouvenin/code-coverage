// ------------------------------------------------------------------------------------------------
// <copyright file="JobHeartbeat.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>Renews the lease of a worker on the job it runs, every <see cref="Interval" />, until the job ends.</summary>
/// <param name="scopeFactory">The factory of the scopes in which the lease is renewed.</param>
/// <param name="timeProvider">The time provider of the renewal timer.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     The lease lasts <see cref="SqlJobQueue.LeaseDuration" /> (5 minutes): renewed every minute, it only
///     expires when the worker is gone, and the <see cref="LeaseSweeper" /> then gives the job back.
///   </para>
///   <para>
///     Each renewal runs in a scope of its own: the database context of the job is busy with the job and
///     cannot be shared. A failed renewal (database unreachable) is logged and tried again at the next
///     tick, while the lease still runs. When the queue answers that the worker no longer holds the job, the
///     heartbeat reports the lost lease and stops: the job is canceled and its outcome is never written.
///   </para>
/// </remarks>
internal sealed partial class JobHeartbeat(
  IServiceScopeFactory scopeFactory,
  TimeProvider timeProvider,
  ILogger<JobHeartbeat> logger)
{
  #region Fields

  /// <summary>The period between two renewals of the lease.</summary>
  public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

  #endregion

  #region Methods

  /// <summary>Renews the lease on a job until the token is canceled or the lease is lost.</summary>
  /// <param name="jobId">The identifier of the running job.</param>
  /// <param name="lockedBy">The worker that holds the job.</param>
  /// <param name="onLeaseLost">Called once when the worker no longer holds the job.</param>
  /// <param name="cancellationToken">A token canceled when the job ends.</param>
  /// <returns>A task that completes when the job ends or the lease is lost; it never faults.</returns>
  /// <exception cref="ArgumentException"><paramref name="lockedBy" /> is <see langword="null" />, empty or white space.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="onLeaseLost" /> is <see langword="null" />.</exception>
  public async Task KeepAliveAsync(long jobId, string lockedBy, Action onLeaseLost, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(lockedBy);
    ArgumentNullException.ThrowIfNull(onLeaseLost);

    using var timer = new PeriodicTimer(Interval, timeProvider);

    try
    {
      while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
      {
        if (!await TryRenewAsync(jobId, lockedBy, cancellationToken).ConfigureAwait(false))
        {
          LogLeaseLost(logger, jobId, lockedBy);
          onLeaseLost();

          return;
        }
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // The job ended.
    }
  }

  /// <summary>Renews the lease once.</summary>
  /// <param name="jobId">The identifier of the running job.</param>
  /// <param name="lockedBy">The worker that holds the job.</param>
  /// <param name="cancellationToken">A token canceled when the job ends.</param>
  /// <returns>
  ///   <see langword="false" /> when the worker no longer holds the job; otherwise <see langword="true" />,
  ///   the renewal failing included (tried again at the next tick).
  /// </returns>
  private async Task<bool> TryRenewAsync(long jobId, string lockedBy, CancellationToken cancellationToken)
  {
    try
    {
      var scope = scopeFactory.CreateAsyncScope();

      await using (scope.ConfigureAwait(false))
      {
        return await scope.ServiceProvider.GetRequiredService<SqlJobQueue>()
                          .ExtendLeaseAsync(jobId, lockedBy, cancellationToken)
                          .ConfigureAwait(false);
      }
    }
    catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    {
      LogRenewalFailed(logger, exception, jobId);

      return true;
    }
  }

  [LoggerMessage(
    EventId = 1210,
    Level = LogLevel.Warning,
    Message = "Job {JobId} is no longer held by the worker {LockedBy}: its lease was lost, the job is canceled"
  )]
  private static partial void LogLeaseLost(ILogger logger, long jobId, string lockedBy);

  [LoggerMessage(
    EventId = 1211,
    Level = LogLevel.Warning,
    Message = "The lease of job {JobId} could not be renewed; trying again at the next heartbeat"
  )]
  private static partial void LogRenewalFailed(ILogger logger, Exception exception, long jobId);

  #endregion
}
