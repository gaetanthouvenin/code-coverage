// ------------------------------------------------------------------------------------------------
// <copyright file="ManualClock.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Tests.Support;

/// <summary>A clock that a test moves forward.</summary>
/// <param name="start">The first time it gives.</param>
public sealed class ManualClock(DateTimeOffset start) : IClock
{
  #region Fields

  private long _ticks = start.UtcTicks;

  #endregion

  #region Properties

  public DateTimeOffset UtcNow => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

  #endregion

  #region Methods

  public void Advance(TimeSpan delay) => Interlocked.Add(ref _ticks, delay.Ticks);

  #endregion
}
