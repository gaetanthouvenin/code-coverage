// ------------------------------------------------------------------------------------------------
// <copyright file="RecurringJobScheduler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   Queues the recurring jobs of the <see cref="RecurringJobCatalog" />: at startup, then every
///   <see cref="Interval" />, each definition queues the run of its next slot.
/// </summary>
/// <remarks>
///   <para>
///     The scheduler keeps no state and takes no lock: every replica of the Api schedules, and the
///     <see cref="RecurringJobDefinition.DedupKey" /> is the only guarantee of uniqueness. Each request is
///     coalesced: while the job of the next slot is queued, the requests of every replica are dropped by
///     <see cref="SqlJobQueue" />, and two replicas that insert it at the same time are kept to one job by the
///     unique index on the queued jobs. Once the job runs, it no longer blocks a request, so the next tick
///     queues the slot after.
///   </para>
///   <para>
///     Runs only where the worker runs (<see cref="WorkerOptions.Enabled" />). A failed definition is logged
///     and tried again at the next tick, without stopping the others: the scheduler never stops the host.
///   </para>
/// </remarks>
internal sealed partial class RecurringJobScheduler : BackgroundService
{
  #region Fields

  /// <summary>The period between two schedulings.</summary>
  public static readonly TimeSpan Interval = RecurringJobDefinition.MinInterval;

  private readonly IReadOnlyList<RecurringJobDefinition> _definitions;

  private readonly ILogger<RecurringJobScheduler> _logger;

  private readonly IOptions<WorkerOptions> _options;

  private readonly IServiceScopeFactory _scopeFactory;

  private readonly TimeProvider _timeProvider;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="RecurringJobScheduler" /> class.</summary>
  /// <param name="definitions">The recurring jobs, from the <see cref="RecurringJobCatalog" />.</param>
  /// <param name="scopeFactory">The factory of the scopes in which the jobs are queued.</param>
  /// <param name="options">The worker options, read once at startup.</param>
  /// <param name="timeProvider">The time provider of the scheduling timer.</param>
  /// <param name="logger">The logger.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="definitions" /> holds a <see langword="null" /> definition, or two definitions with the
  ///   same <see cref="RecurringJobDefinition.DedupKey" />.
  /// </exception>
  public RecurringJobScheduler(
    IEnumerable<RecurringJobDefinition> definitions,
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<RecurringJobScheduler> logger)
  {
    ArgumentNullException.ThrowIfNull(definitions);
    ArgumentNullException.ThrowIfNull(scopeFactory);
    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(timeProvider);
    ArgumentNullException.ThrowIfNull(logger);

    var list = definitions.ToList();

    ArgumentException.ThrowIf(list.Contains(null!), "A recurring job definition is null.", nameof(definitions));

    var duplicate = list.GroupBy(static d => d.DedupKey, StringComparer.Ordinal).FirstOrDefault(static g => g.Count() > 1);

    if (duplicate is not null)
    {
      throw new ArgumentException(
        $"Two recurring jobs share the deduplication key '{duplicate.Key}': it must be unique.",
        nameof(definitions)
      );
    }

    _definitions = list;
    _scopeFactory = scopeFactory;
    _options = options;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  #endregion

  #region Properties

  /// <summary>Gets the recurring jobs this scheduler queues.</summary>
  internal IReadOnlyList<RecurringJobDefinition> Definitions => _definitions;

  #endregion

  #region Methods

  /// <summary>Queues the next run of every recurring job once.</summary>
  /// <param name="cancellationToken">A token to cancel the scheduling.</param>
  /// <returns>How many definitions were scheduled: queued, or merged into their queued twin.</returns>
  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
  /// <remarks>A failed definition is logged and skipped: the others are still scheduled.</remarks>
  internal async Task<int> ScheduleAsync(CancellationToken cancellationToken)
  {
    if (_definitions.Count == 0)
    {
      return 0;
    }

    var scope = _scopeFactory.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
      var clock = scope.ServiceProvider.GetRequiredService<IClock>();
      var scheduled = 0;

      foreach (var definition in _definitions)
      {
        try
        {
          await queue.EnqueueAsync(definition.ToRequest(clock.UtcNow), cancellationToken).ConfigureAwait(false);
          scheduled++;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
          LogScheduleFailed(_logger, definition.Type, exception);
        }
      }

      return scheduled;
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

    if (_definitions.Count == 0)
    {
      LogNothingToSchedule(_logger);
      return;
    }

    using var timer = new PeriodicTimer(Interval, _timeProvider);

    try
    {
      // At startup, then at each tick: a replica that starts queues the missing jobs at once.
      do
      {
        await ScheduleAsync(stoppingToken).ConfigureAwait(false);
      }
      while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // The host is stopping.
    }
  }

  [LoggerMessage(EventId = 1230, Level = LogLevel.Information, Message = "Worker:Enabled is false: the recurring job scheduler is idle")]
  private static partial void LogDisabled(ILogger logger);

  [LoggerMessage(EventId = 1231, Level = LogLevel.Information, Message = "No recurring job is defined: the recurring job scheduler is idle")]
  private static partial void LogNothingToSchedule(ILogger logger);

  [LoggerMessage(
    EventId = 1232,
    Level = LogLevel.Warning,
    Message = "The scheduling of the recurring job {JobType} failed; trying again at the next tick"
  )]
  private static partial void LogScheduleFailed(ILogger logger, string jobType, Exception exception);

  #endregion
}
