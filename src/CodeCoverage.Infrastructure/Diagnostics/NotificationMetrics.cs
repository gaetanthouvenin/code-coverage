// ------------------------------------------------------------------------------------------------
// <copyright file="NotificationMetrics.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Diagnostics;

/// <summary><see cref="INotificationMetrics" /> over the meter of the application.</summary>
/// <param name="meter">The meter.</param>
internal sealed class NotificationMetrics(CodeCoverageMeter meter) : INotificationMetrics
{
  #region Implementation of INotificationMetrics

  /// <inheritdoc />
  public void NotifyLatency(TimeSpan latency) => meter.NotifyLatency(latency < TimeSpan.Zero ? TimeSpan.Zero : latency);

  #endregion
}