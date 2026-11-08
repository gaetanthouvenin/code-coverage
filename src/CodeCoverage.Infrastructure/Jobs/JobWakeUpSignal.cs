// ------------------------------------------------------------------------------------------------
// <copyright file="JobWakeUpSignal.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Threading.Channels;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   Wakes an idle loop of the <see cref="JobWorker" /> as soon as a job is queued in the same process,
///   instead of waiting for its next poll.
/// </summary>
/// <remarks>
///   <para>
///     A bounded <see cref="Channel{T}" /> of job identifiers: each notification wakes one waiting loop. The
///     channel drops its oldest notification when it is full, so a process that queues jobs and runs no
///     worker (the Web) never holds more than <see cref="Capacity" /> of them.
///   </para>
///   <para>
///     The signal is a shortcut, never the only way to see a job: a job queued by another process, or one
///     whose notification was dropped, is found by the next poll of the worker.
///   </para>
/// </remarks>
internal sealed class JobWakeUpSignal
{
  #region Fields

  /// <summary>How many notifications the channel keeps at most.</summary>
  public const int Capacity = 64;

  private readonly Channel<long> _channel = Channel.CreateBounded<long>(
    new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest }
  );

  private readonly TimeProvider _timeProvider;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="JobWakeUpSignal" /> class.</summary>
  /// <param name="timeProvider">The clock that measures the wait timeout, the same as the rest of the worker.</param>
  /// <exception cref="ArgumentNullException"><paramref name="timeProvider" /> is <see langword="null" />.</exception>
  public JobWakeUpSignal(TimeProvider timeProvider)
  {
    ArgumentNullException.ThrowIfNull(timeProvider);
    _timeProvider = timeProvider;
  }

  #endregion

  #region Methods

  /// <summary>Notifies the worker that a job was queued and committed.</summary>
  /// <param name="jobId">The identifier of the queued job.</param>
  /// <remarks>Never blocks: called after the commit of the change that queued the job.</remarks>
  public void Notify(long jobId) => _channel.Writer.TryWrite(jobId);

  /// <summary>Waits for a notification, at most for a given time.</summary>
  /// <param name="timeout">How long to wait at most.</param>
  /// <param name="cancellationToken">A token to stop waiting.</param>
  /// <returns>
  ///   <see langword="true" /> when a notification arrived; <see langword="false" /> when the time ran out.
  /// </returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout" /> is negative.</exception>
  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
  public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
    cancellationToken.ThrowIfCancellationRequested();

    // A pending notification is taken at once, whatever the timeout.
    if (_channel.Reader.TryRead(out _))
    {
      return true;
    }

    // The deadline runs on the injected clock, so a fake clock drives the idle poll like the rest of the worker.
    using var timer = new CancellationTokenSource(timeout, _timeProvider);
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);

    try
    {
      while (await _channel.Reader.WaitToReadAsync(deadline.Token).ConfigureAwait(false))
      {
        // Another loop may take the notification first: wait for the next one.
        if (_channel.Reader.TryRead(out _))
        {
          return true;
        }
      }

      return false;
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      return false;
    }
  }

  #endregion
}
