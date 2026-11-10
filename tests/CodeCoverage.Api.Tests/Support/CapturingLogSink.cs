// ------------------------------------------------------------------------------------------------
// <copyright file="CapturingLogSink.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Globalization;

using Serilog.Core;
using Serilog.Events;

namespace CodeCoverage.Api.Tests.Support;

// Keeps every event of the host, rendered with its properties and its exception: Serilog reads the
// sinks registered in the container (ReadFrom.Services).
public sealed class CapturingLogSink : ILogEventSink
{
  #region Fields

  private readonly ConcurrentQueue<string> _events = new();

  #endregion

  #region Properties

  public IReadOnlyCollection<string> Events => _events;

  #endregion

  #region Implementation of ILogEventSink

  public void Emit(LogEvent logEvent)
  {
    var properties = string.Join(' ', logEvent.Properties.Select(static p => $"{p.Key}={p.Value}"));

    _events.Enqueue($"{logEvent.RenderMessage(CultureInfo.InvariantCulture)} {properties} {logEvent.Exception}");
  }

  #endregion
}
