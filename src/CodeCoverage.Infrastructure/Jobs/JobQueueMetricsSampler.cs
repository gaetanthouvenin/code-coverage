// ------------------------------------------------------------------------------------------------
// <copyright file="JobQueueMetricsSampler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using CodeCoverage.Infrastructure.Diagnostics;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   Measures the job queue every <see cref="Interval" /> and reports it to the <see cref="CodeCoverageMeter" />,
///   whose gauges <see cref="CodeCoverageMeter.JobsQueued" /> and
///   <see cref="CodeCoverageMeter.JobOldestQueuedAgeSeconds" /> publish the last measure.
/// </summary>
/// <remarks>
///   <para>
///     The measure is done by <see cref="SqlJobQueue.MeasureAsync" />, in a scope of its own: a collection of the
///     metrics reads the last measure and never queries the database.
///   </para>
///   <para>
///     No measure is done while nobody collects the gauges (<see cref="CodeCoverageMeter.IsJobQueueObserved" />):
///     without an OTLP endpoint, the sampler neither queries the database nor logs a failed measure.
///   </para>
///   <para>
///     Registered by the Api only (<see cref="DependencyInjection.AddJobProcessing" />). It measures even when
///     <c>Worker:Enabled</c> is <see langword="false" />: the queue is shared by every replica, and a queue that
///     nobody runs is exactly what the gauges must show. A failed measure is logged and tried again at the next
///     tick: the sampler never stops the host.
///   </para>
/// </remarks>
internal sealed partial class JobQueueMetricsSampler : BackgroundService
{
  #region Fields

  /// <summary>The period between two measures.</summary>
  public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

  private readonly ILogger<JobQueueMetricsSampler> _logger;

  private readonly CodeCoverageMeter _meter;

  private readonly IServiceScopeFactory _scopeFactory;

  private readonly TimeProvider _timeProvider;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="JobQueueMetricsSampler" /> class.</summary>
  /// <param name="scopeFactory">The factory of the scopes in which the measures run.</param>
  /// <param name="meter">The metrics of the application, to which the measures are reported.</param>
  /// <param name="timeProvider">The time provider of the timer.</param>
  /// <param name="logger">The logger.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public JobQueueMetricsSampler(
    IServiceScopeFactory scopeFactory,
    CodeCoverageMeter meter,
    TimeProvider timeProvider,
    ILogger<JobQueueMetricsSampler> logger)
  {
    ArgumentNullException.ThrowIfNull(scopeFactory);
    ArgumentNullException.ThrowIfNull(meter);
    ArgumentNullException.ThrowIfNull(timeProvider);
    ArgumentNullException.ThrowIfNull(logger);

    _scopeFactory = scopeFactory;
    _meter = meter;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  #endregion

  #region Methods

  /// <summary>Measures the queue once and reports the measure to the meter, when a listener collects the gauges.</summary>
  /// <param name="cancellationToken">A token to cancel the measure.</param>
  /// <returns>
  ///   The measure, or <see langword="null" /> when nobody collects the gauges of the jobs and no measure was done.
  /// </returns>
  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
  /// <remarks>A failed measure is not caught here: the loop of the service logs it.</remarks>
  internal async Task<JobQueueSnapshot?> SampleAsync(CancellationToken cancellationToken)
  {
    if (!_meter.IsJobQueueObserved)
    {
      return null;
    }

    var scope = _scopeFactory.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      var snapshot = await scope.ServiceProvider.GetRequiredService<SqlJobQueue>()
                                .MeasureAsync(cancellationToken)
                                .ConfigureAwait(false);

      _meter.ReportJobQueue(snapshot);

      return snapshot;
    }
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(Interval, _timeProvider);

    try
    {
      while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
      {
        try
        {
          await SampleAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
          LogSampleFailed(_logger, exception);
        }
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // The host is stopping.
    }
  }

  [LoggerMessage(EventId = 1240, Level = LogLevel.Warning, Message = "The measure of the job queue failed; trying again at the next tick")]
  private static partial void LogSampleFailed(ILogger logger, Exception exception);

  #endregion
}
