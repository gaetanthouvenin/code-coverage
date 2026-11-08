// ------------------------------------------------------------------------------------------------
// <copyright file="SystemClock.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Time;

/// <summary>The clock of the host, read through a <see cref="TimeProvider" />.</summary>
/// <param name="timeProvider">The time provider (<see cref="TimeProvider.System" /> outside tests).</param>
internal sealed class SystemClock(TimeProvider timeProvider) : IClock
{
  #region Implementation of IClock

  /// <inheritdoc />
  public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

  #endregion
}
