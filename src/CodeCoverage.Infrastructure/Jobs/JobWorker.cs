// ------------------------------------------------------------------------------------------------
// <copyright file="JobWorker.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.Identity;

using AppException = CodeCoverage.Application.Exceptions.ApplicationException;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   Runs the background jobs of the <c>Jobs</c> table in the Api: <c>Processing:WorkerConcurrency</c> loops
///   that claim a job, run it as the system principal and record its outcome.
/// </summary>
/// <remarks>
///   <para>
///     <b>Loops.</b> <c>Processing:WorkerConcurrency</c> loops (4 by default, 1 to 16, read at startup: the
///     setting requires a restart), each with its own worker identifier. A loop claims jobs as long as it finds
///     some; when the queue is empty, it waits for the <see cref="JobWakeUpSignal" /> of a job queued in this
///     process, or <see cref="PollInterval" /> at most, which also covers the jobs queued by the Web or by another
///     replica. A loop that fails (database unreachable) logs the error and tries again after the same wait: the
///     worker never stops the host.
///   </para>
///   <para>
///     <b>Execution.</b> Each job runs in a new DI scope whose principal is the system
///     (<see cref="CurrentUserContext.SetSystem" />), within a logging scope (<c>JobId</c>, <c>JobType</c>,
///     <c>Attempt</c>, <c>CorrelationId</c>) and an <see cref="Activity" /> of
///     <see cref="CodeCoverageActivitySources.Jobs" />. Its <see cref="IJobDispatcher" /> sends the command of its
///     type. The job is canceled after <see cref="JobTimeout" />, when the <see cref="JobHeartbeat" /> that renews
///     its lease finds it lost, or when the host stops.
///   </para>
///   <para><b>Outcome.</b> Every write is conditioned on the worker still holding the job:</para>
///   <list type="bullet">
///     <item>success: <see cref="SqlJobQueue.CompleteAsync" />;</item>
///     <item>
///       <see cref="RetryLaterException" />: queued again after its delay, without consuming the attempt
///       (<see cref="SqlJobQueue.RescheduleAsync" />);
///     </item>
///     <item>
///       permanent refusal (<see cref="NotFoundException" />, <see cref="ValidationException" />,
///       <see cref="BadRequestException" />, <see cref="UnprocessableEntityException" />,
///       <see cref="InvalidJobException" />): failed for good, never retried;
///     </item>
///     <item>
///       any other error, the timeout included: queued again after the <see cref="JobRetryPolicy" /> backoff, or
///       dead once its attempts are used (<see cref="SqlJobQueue.FailAsync" />);
///     </item>
///     <item>host stopping: queued again at once, without consuming the attempt;</item>
///     <item>lease lost: nothing is written, the job belongs to another worker now.</item>
///   </list>
///   <para>
///     Registered by the Api only (<see cref="DependencyInjection.AddJobProcessing" />), and idle when
///     <c>Worker:Enabled</c> is <see langword="false" />.
///   </para>
/// </remarks>
internal sealed partial class JobWorker : BackgroundService
{
  #region Fields

  /// <summary>The longest wait of an idle loop before it looks at the queue again.</summary>
  public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

  /// <summary>How long a job may run before it is canceled.</summary>
  public static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(10);

  /// <summary>The longest text kept as the last error of a job.</summary>
  internal const int MaxErrorLength = 4000;

  /// <summary>How long the outcome of a job may take to be written, even while the host stops.</summary>
  private static readonly TimeSpan OutcomeTimeout = TimeSpan.FromSeconds(30);

  /// <summary>The number of loops when <c>Processing:WorkerConcurrency</c> is missing or invalid.</summary>
  private static readonly SettingDefinition ConcurrencyDefinition =
    SettingDefinitions.Find(SettingDefinitions.ProcessingWorkerConcurrency)!;

  private readonly IClock _clock;

  private readonly IConfiguration _configuration;

  private readonly JobHeartbeat _heartbeat;

  private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

  private readonly ILogger<JobWorker> _logger;

  private readonly IOptions<WorkerOptions> _options;

  private readonly IServiceScopeFactory _scopeFactory;

  private readonly JobWakeUpSignal _signal;

  private readonly TimeProvider _timeProvider;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="JobWorker" /> class.</summary>
  /// <param name="scopeFactory">The factory of the scopes of the claims, the jobs and the outcomes.</param>
  /// <param name="configuration">The running configuration, read at startup for <c>Processing:WorkerConcurrency</c>.</param>
  /// <param name="options">The worker options, read once at startup.</param>
  /// <param name="signal">The signal of the jobs queued in this process.</param>
  /// <param name="heartbeat">The heartbeat that renews the lease of the running jobs.</param>
  /// <param name="clock">The clock that dates the retries.</param>
  /// <param name="timeProvider">The time provider of the timeouts.</param>
  /// <param name="logger">The logger.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public JobWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IOptions<WorkerOptions> options,
    JobWakeUpSignal signal,
    JobHeartbeat heartbeat,
    IClock clock,
    TimeProvider timeProvider,
    ILogger<JobWorker> logger)
  {
    ArgumentNullException.ThrowIfNull(scopeFactory);
    ArgumentNullException.ThrowIfNull(configuration);
    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(signal);
    ArgumentNullException.ThrowIfNull(heartbeat);
    ArgumentNullException.ThrowIfNull(clock);
    ArgumentNullException.ThrowIfNull(timeProvider);
    ArgumentNullException.ThrowIfNull(logger);

    _scopeFactory = scopeFactory;
    _configuration = configuration;
    _options = options;
    _signal = signal;
    _heartbeat = heartbeat;
    _clock = clock;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  #endregion

  #region Methods

  /// <summary>Reads how many loops the worker runs.</summary>
  /// <param name="configuration">The running configuration.</param>
  /// <returns>
  ///   <c>Processing:WorkerConcurrency</c> when it is a number, brought within its bounds (1 to 16); otherwise
  ///   its default (4).
  /// </returns>
  internal static int ReadConcurrency(IConfiguration configuration)
  {
    var fallback = int.Parse(ConcurrencyDefinition.Default!, CultureInfo.InvariantCulture);

    if (!int.TryParse(
          configuration[SettingDefinitions.ProcessingWorkerConcurrency],
          NumberStyles.Integer,
          CultureInfo.InvariantCulture,
          out var concurrency
        ))
    {
      return fallback;
    }

    var min = ConcurrencyDefinition.Min ?? 1;
    var max = ConcurrencyDefinition.Max ?? fallback;

    return Math.Clamp(concurrency, min, max);
  }

  /// <summary>Claims the next job that may run and runs it to its outcome.</summary>
  /// <param name="workerId">The identifier of the loop, written as the holder of the job.</param>
  /// <param name="stoppingToken">A token canceled when the host stops.</param>
  /// <returns><see langword="true" /> when a job was run; <see langword="false" /> when none may run now.</returns>
  /// <exception cref="ArgumentException"><paramref name="workerId" /> is <see langword="null" />, empty or white space.</exception>
  /// <remarks>
  ///   The failures of the job are its outcome, never thrown. The failures of the queue (claim, outcome) are
  ///   thrown: the loop logs them, and a job left running is given back by the <see cref="LeaseSweeper" />.
  /// </remarks>
  internal async Task<bool> RunOnceAsync(string workerId, CancellationToken stoppingToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

    var job = await WithQueueAsync(q => q.ClaimNextAsync(workerId, stoppingToken)).ConfigureAwait(false);

    if (job is null)
    {
      return false;
    }

    await RunAsync(job, workerId, stoppingToken).ConfigureAwait(false);

    return true;
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!_options.Value.Enabled)
    {
      LogDisabled(_logger);
      return;
    }

    var concurrency = ReadConcurrency(_configuration);
    LogStarting(_logger, concurrency);

    var loops = Enumerable.Range(1, concurrency).Select(i => LoopAsync(WorkerId(i), stoppingToken)).ToArray();
    await Task.WhenAll(loops).ConfigureAwait(false);
  }

  /// <summary>Describes the error of a failed attempt, as kept in the <c>Jobs</c> table.</summary>
  /// <param name="exception">The error.</param>
  /// <returns>The error code (or type) and the message of the error, cut to <see cref="MaxErrorLength" />.</returns>
  private static string Describe(Exception exception)
  {
    var text = exception is AppException refusal
                 ? $"{refusal.ErrorCode}: {refusal.Message}"
                 : $"{exception.GetType().Name}: {exception.Message}";

    return text.Length <= MaxErrorLength ? text : text[..MaxErrorLength];
  }

  /// <summary>Tells whether running a job again would fail the same way.</summary>
  /// <param name="exception">The error of the job.</param>
  /// <returns><see langword="true" /> for a refusal of the Application or an invalid job.</returns>
  private static bool IsPermanent(Exception exception) =>
    exception is NotFoundException
                 or ValidationException
                 or BadRequestException
                 or UnprocessableEntityException
                 or InvalidJobException;

  /// <summary>Starts the activity of a job.</summary>
  /// <param name="job">The job.</param>
  /// <returns>The activity, or <see langword="null" /> when nobody listens.</returns>
  private static Activity? StartActivity(JobRecord job)
  {
    var activity = CodeCoverageActivitySources.Jobs.StartActivity($"Job {job.Type}", ActivityKind.Consumer);

    activity?.SetTag("job.id", job.Id)
             .SetTag("job.type", job.Type)
             .SetTag("job.attempt", job.Attempts)
             .SetTag("job.correlation_id", job.CorrelationId);

    return activity;
  }

  /// <summary>Runs one job in a new scope, as the system principal.</summary>
  /// <param name="job">The job.</param>
  /// <param name="cancellationToken">The token of the job.</param>
  /// <returns>A task that completes when the command of the job was handled.</returns>
  private async Task DispatchAsync(JobRecord job, CancellationToken cancellationToken)
  {
    var scope = _scopeFactory.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      scope.ServiceProvider.GetRequiredService<CurrentUserContext>().SetSystem();

      await scope.ServiceProvider.GetRequiredService<IJobDispatcher>()
                 .DispatchAsync(job, cancellationToken)
                 .ConfigureAwait(false);
    }
  }

  /// <summary>Runs a loop until the host stops.</summary>
  /// <param name="workerId">The identifier of the loop.</param>
  /// <param name="stoppingToken">A token canceled when the host stops.</param>
  /// <returns>A task that completes when the host stops.</returns>
  private async Task LoopAsync(string workerId, CancellationToken stoppingToken)
  {
    // Never run the first claim synchronously in ExecuteAsync: each loop starts on its own.
    await Task.Yield();

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        if (await RunOnceAsync(workerId, stoppingToken).ConfigureAwait(false))
        {
          continue;
        }

        await _signal.WaitAsync(PollInterval, stoppingToken).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        return;
      }
      catch (Exception exception)
      {
        LogLoopFailed(_logger, exception, workerId);

        try
        {
          await Task.Delay(PollInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
          return;
        }
      }
    }
  }

  /// <summary>Records the outcome of a job.</summary>
  /// <param name="job">The job.</param>
  /// <param name="workerId">The worker that holds the job.</param>
  /// <param name="failure">The error of the job, or <see langword="null" /> when it succeeded.</param>
  /// <param name="cancellation">Why the job was canceled, when it was.</param>
  /// <param name="cancellationToken">A token to cancel the writing.</param>
  /// <returns>A task that completes when the outcome is written.</returns>
  private async Task RecordAsync(
    JobRecord job,
    string workerId,
    Exception? failure,
    JobCancellation cancellation,
    CancellationToken cancellationToken)
  {
    var now = _clock.UtcNow;

    if (failure is null)
    {
      if (!await WithQueueAsync(q => q.CompleteAsync(job.Id, workerId, cancellationToken)).ConfigureAwait(false))
      {
        LogOutcomeDiscarded(_logger, job.Id);
      }

      return;
    }

    if (failure is OperationCanceledException && cancellation == JobCancellation.LeaseLost)
    {
      // Another worker holds the job now (or will): its outcome is not ours to write.
      LogOutcomeDiscarded(_logger, job.Id);
      return;
    }

    if (failure is OperationCanceledException && cancellation == JobCancellation.HostStopping)
    {
      LogRequeuedOnStop(_logger, job.Id);
      await WithQueueAsync(q => q.RescheduleAsync(job.Id, workerId, now, "The host stopped while the job ran.", cancellationToken))
        .ConfigureAwait(false);
      return;
    }

    if (failure is RetryLaterException retryLater)
    {
      var runAfter = now + retryLater.Delay;
      LogRetryLater(_logger, job.Id, runAfter, retryLater.Message);
      await WithQueueAsync(q => q.RescheduleAsync(job.Id, workerId, runAfter, Describe(retryLater), cancellationToken))
        .ConfigureAwait(false);
      return;
    }

    var error = failure is OperationCanceledException && cancellation == JobCancellation.TimedOut
                  ? $"The job did not end within {JobTimeout.TotalMinutes.ToString(CultureInfo.InvariantCulture)} minutes."
                  : Describe(failure);

    if (IsPermanent(failure))
    {
      LogFailed(_logger, failure, job.Id, error);
      await WithQueueAsync(q => q.FailAsync(job.Id, workerId, error, retryAt: null, cancellationToken)).ConfigureAwait(false);
      return;
    }

    if (job.Attempts >= job.MaxAttempts)
    {
      LogDead(_logger, failure, job.Id, job.Attempts, error);
    }
    else
    {
      LogRetrying(_logger, failure, job.Id, job.Attempts, job.MaxAttempts, error);
    }

    var retryAt = now + JobRetryPolicy.NextDelay(job.Attempts);
    await WithQueueAsync(q => q.FailAsync(job.Id, workerId, error, retryAt, cancellationToken)).ConfigureAwait(false);
  }

  /// <summary>Runs a claimed job to its outcome.</summary>
  /// <param name="job">The job, held by <paramref name="workerId" />.</param>
  /// <param name="workerId">The worker that holds the job.</param>
  /// <param name="stoppingToken">A token canceled when the host stops.</param>
  /// <returns>A task that completes when the outcome of the job is written.</returns>
  private async Task RunAsync(JobRecord job, string workerId, CancellationToken stoppingToken)
  {
    var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
      ["JobId"] = job.Id,
      ["JobType"] = job.Type,
      ["Attempt"] = job.Attempts,
      ["CorrelationId"] = job.CorrelationId,
    };

    using var logScope = _logger.BeginScope(properties);
    using var activity = StartActivity(job);
    using var leaseLost = new CancellationTokenSource();
    using var timeout = new CancellationTokenSource(JobTimeout, _timeProvider);
    using var jobToken = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, leaseLost.Token, timeout.Token);
    using var heartbeatStop = new CancellationTokenSource();

    var heartbeat = _heartbeat.KeepAliveAsync(job.Id, workerId, leaseLost.Cancel, heartbeatStop.Token);
    var started = _timeProvider.GetTimestamp();
    Exception? failure = null;

    try
    {
      await DispatchAsync(job, jobToken.Token).ConfigureAwait(false);
    }
    catch (Exception exception)
    {
      failure = exception;
    }
    finally
    {
      await heartbeatStop.CancelAsync().ConfigureAwait(false);
      await heartbeat.ConfigureAwait(false);
    }

    var cancellation = leaseLost.IsCancellationRequested ? JobCancellation.LeaseLost
                       : stoppingToken.IsCancellationRequested ? JobCancellation.HostStopping
                       : timeout.IsCancellationRequested ? JobCancellation.TimedOut
                       : JobCancellation.None;

    if (failure is null)
    {
      activity?.SetStatus(ActivityStatusCode.Ok);
      LogSucceeded(_logger, job.Id, job.Type, _timeProvider.GetElapsedTime(started).TotalMilliseconds);
    }
    else
    {
      activity?.SetStatus(ActivityStatusCode.Error, failure.GetType().Name);
    }

    // The outcome is written even while the host stops, within a bounded time.
    using var outcome = new CancellationTokenSource(OutcomeTimeout, _timeProvider);
    await RecordAsync(job, workerId, failure, cancellation, outcome.Token).ConfigureAwait(false);
  }

  /// <summary>Runs an operation of the queue in a scope of its own.</summary>
  /// <typeparam name="T">The type of the result.</typeparam>
  /// <param name="operation">The operation.</param>
  /// <returns>The result of the operation.</returns>
  private async Task<T> WithQueueAsync<T>(Func<SqlJobQueue, Task<T>> operation)
  {
    var scope = _scopeFactory.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      return await operation(scope.ServiceProvider.GetRequiredService<SqlJobQueue>()).ConfigureAwait(false);
    }
  }

  /// <summary>Builds the identifier of a loop, unique across the replicas and the restarts.</summary>
  /// <param name="index">The number of the loop, from 1.</param>
  /// <returns>The machine, the process, the instance of the worker and the loop.</returns>
  private string WorkerId(int index)
  {
    var suffix = string.Create(CultureInfo.InvariantCulture, $":{Environment.ProcessId}:{_instanceId}:{index}");
    var machine = Environment.MachineName;
    var room = JobRecord.LockedByMaxLength - suffix.Length;

    return (machine.Length <= room ? machine : machine[..room]) + suffix;
  }

  [LoggerMessage(EventId = 1200, Level = LogLevel.Information, Message = "Worker:Enabled is false: the job worker is idle")]
  private static partial void LogDisabled(ILogger logger);

  [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Job worker starting with {Concurrency} loop(s)")]
  private static partial void LogStarting(ILogger logger, int concurrency);

  [LoggerMessage(EventId = 1202, Level = LogLevel.Information, Message = "Job {JobId} ({JobType}) succeeded in {ElapsedMilliseconds} ms")]
  private static partial void LogSucceeded(ILogger logger, long jobId, string jobType, double elapsedMilliseconds);

  [LoggerMessage(EventId = 1203, Level = LogLevel.Information, Message = "Job {JobId} runs again at {RunAfter} without consuming an attempt: {Reason}")]
  private static partial void LogRetryLater(ILogger logger, long jobId, DateTimeOffset runAfter, string reason);

  [LoggerMessage(EventId = 1204, Level = LogLevel.Warning, Message = "Job {JobId} failed for good: {Error}")]
  private static partial void LogFailed(ILogger logger, Exception exception, long jobId, string error);

  [LoggerMessage(
    EventId = 1205,
    Level = LogLevel.Warning,
    Message = "Job {JobId} failed on attempt {Attempt} of {MaxAttempts}, it will run again: {Error}"
  )]
  private static partial void LogRetrying(ILogger logger, Exception exception, long jobId, int attempt, int maxAttempts, string error);

  [LoggerMessage(EventId = 1206, Level = LogLevel.Error, Message = "Job {JobId} failed on its last attempt ({Attempt}) and is dead: {Error}")]
  private static partial void LogDead(ILogger logger, Exception exception, long jobId, int attempt, string error);

  [LoggerMessage(
    EventId = 1207,
    Level = LogLevel.Warning,
    Message = "The outcome of job {JobId} is discarded: the worker no longer holds the job"
  )]
  private static partial void LogOutcomeDiscarded(ILogger logger, long jobId);

  [LoggerMessage(EventId = 1208, Level = LogLevel.Information, Message = "The host stops: job {JobId} is queued again")]
  private static partial void LogRequeuedOnStop(ILogger logger, long jobId);

  [LoggerMessage(EventId = 1209, Level = LogLevel.Warning, Message = "The job loop {WorkerId} failed; trying again shortly")]
  private static partial void LogLoopFailed(ILogger logger, Exception exception, string workerId);

  #endregion
}
