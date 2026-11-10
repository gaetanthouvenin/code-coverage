// ------------------------------------------------------------------------------------------------
// <copyright file="INotificationMetrics.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>Measures the notifications.</summary>
public interface INotificationMetrics
{
  #region Methods

  /// <summary>Records the time between the last upload of a commit and the publication of its statuses (<c>notify_latency_seconds</c>).</summary>
  /// <param name="latency">The time elapsed.</param>
  void NotifyLatency(TimeSpan latency);

  #endregion
}