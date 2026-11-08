// ------------------------------------------------------------------------------------------------
// <copyright file="LeaseSweeper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   Gives back, every <see cref="Interval" />, the running jobs whose lease expired: their worker
///   disappeared (crash, killed replica) without ending them.
/// </summary>
/// <remarks>
///   <para>
///     The work is done by <see cref="SqlJobQueue.SweepExpiredLeasesAsync" />, in a scope of its own: an
///     expired job is queued again, or absorbed by its queued twin when it has a <c>DedupKey</c> (the twin
///     does the same work), or marked dead when it used all its attempts.
///   </para>
///   <para>
///     Every replica of the Api sweeps: each update is conditioned on the expired lease, so two sweepers
///     never give back the same job twice. Runs only where the worker runs (<see cref="WorkerOptions.Enabled" />).
///     A failed sweep is logged and tried again at the next tick: the sweeper never stops the host.
///   </para>
/// </remarks>
internal sealed partial class LeaseSweeper : BackgroundService
{
  #region Fields

  /// <summary>The period between two sweeps.</summary>
  public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

  private readonly ILogger<LeaseSweeper> _logger;

  private readonly IOptions<WorkerOptions> _options;

  private readonly IServiceScopeFactory _scopeFactory;

  private readonly TimeProvider _timeProvider;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="LeaseSweeper" /> class.</summary>
  /// <param name="scopeFactory">The factory of the scopes in which the sweeps run.</param>
  /// <param name="options">The worker options, read once at startup.</param>
  /// <param name="timeProvider">The time provider of the sweep timer.</param>
  /// <param name="logger">The logger.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public LeaseSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<LeaseSweeper> logger)
  {
    ArgumentNullException.ThrowIfNull(scopeFactory);
    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(timeProvider);
    ArgumentNullException.ThrowIfNull(logger);

    _scopeFactory = scopeFactory;
    _options = options;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  #endregion

  #region Methods

  /// <summary>Sweeps the expired leases once.</summary>
  /// <param name="cancellationToken">A token to cancel the sweep.</param>
  /// <returns>How many jobs were queued again, absorbed or marked dead.</returns>
  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
  /// <remarks>A failed sweep is not caught here: the loop of the service logs it.</remarks>
  internal async Task<int> SweepAsync(CancellationToken cancellationToken)
  {
    var scope = _scopeFactory.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      var swept = await scope.ServiceProvider.GetRequiredService<SqlJobQueue>()
                             .SweepExpiredLeasesAsync(cancellationToken)
                             .ConfigureAwait(false);

      if (swept > 0)
      {
        LogSwept(_logger, swept);
      }

      return swept;
    }
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!_options.Value.Enabled)
    {
      LogDisabled(_logger);
      return;
    }

    using var timer = new PeriodicTimer(Interval, _timeProvider);

    try
    {
      while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
      {
        try
        {
          await SweepAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
          LogSweepFailed(_logger, exception);
        }
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // The host is stopping.
    }
  }

  [LoggerMessage(EventId = 1220, Level = LogLevel.Information, Message = "Worker:Enabled is false: the lease sweeper is idle")]
  private static partial void LogDisabled(ILogger logger);

  [LoggerMessage(
    EventId = 1221,
    Level = LogLevel.Warning,
    Message = "{Count} job(s) had an expired lease: queued again, absorbed by their twin or marked dead"
  )]
  private static partial void LogSwept(ILogger logger, int count);

  [LoggerMessage(EventId = 1222, Level = LogLevel.Warning, Message = "The sweep of the expired leases failed; trying again at the next tick")]
  private static partial void LogSweepFailed(ILogger logger, Exception exception);

  #endregion
}
