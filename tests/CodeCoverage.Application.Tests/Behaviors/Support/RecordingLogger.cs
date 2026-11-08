// ------------------------------------------------------------------------------------------------
// <copyright file="RecordingLogger.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// A logger that keeps every entry, with its structured values, in a PipelineTrace.
internal sealed class RecordingLogger<T> : ILogger<T>
{
  #region Fields

  private readonly PipelineTrace _trace;

  #endregion

  #region Ctors

  public RecordingLogger(PipelineTrace trace)
  {
    _trace = trace;
  }

  #endregion

  #region Implementation of ILogger

  public IDisposable? BeginScope<TState>(TState state)
    where TState : notnull => null;

  public bool IsEnabled(LogLevel logLevel) => true;

  public void Log<TState>(
    LogLevel logLevel,
    EventId eventId,
    TState state,
    Exception? exception,
    Func<TState, Exception?, string> formatter)
  {
    ArgumentNullException.ThrowIfNull(formatter);

    var message = formatter(state, exception);
    var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];

    lock (_trace)
    {
      _trace.Logs.Add(new LogEntry(logLevel, message, [.. values], exception));
      _trace.Steps.Add($"{logLevel}: {message}");
    }
  }

  #endregion
}
