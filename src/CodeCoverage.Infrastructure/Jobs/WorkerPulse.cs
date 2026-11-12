// ------------------------------------------------------------------------------------------------
// <copyright file="WorkerPulse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>Remembers when a worker loop of this process last turned: the heartbeat shown to the administrators.</summary>
/// <remarks>
///   In process only, without a table: one Api replica runs the worker (decision of the batch, ARCHITECTURE §12.6). A
///   host without a worker, the Web or a replica with <c>Worker:Enabled = false</c>, has never beaten.
/// </remarks>
internal sealed class WorkerPulse
{
  #region Fields

  private long _lastBeatTicks;

  #endregion

  #region Properties

  /// <summary>Gets when a worker loop last turned, in UTC, or <see langword="null" /> when none ever did.</summary>
  public DateTimeOffset? LastBeatAt
  {
    get
    {
      var ticks = Interlocked.Read(ref _lastBeatTicks);

      return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
    }
  }

  #endregion

  #region Methods

  /// <summary>Records that a worker loop turned.</summary>
  /// <param name="at">The current time; converted to UTC.</param>
  public void Beat(DateTimeOffset at) => Interlocked.Exchange(ref _lastBeatTicks, at.UtcTicks);

  #endregion
}
