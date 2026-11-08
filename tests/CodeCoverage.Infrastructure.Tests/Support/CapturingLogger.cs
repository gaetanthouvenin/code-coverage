// ------------------------------------------------------------------------------------------------
// <copyright file="CapturingLogger.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace CodeCoverage.Infrastructure.Tests.Support;

// A logger that keeps the event, the level and the exception of each entry, at every level.
public sealed class CapturingLogger<T> : ILogger<T>
{
  #region Properties

  public ConcurrentQueue<(EventId EventId, LogLevel Level, Exception? Exception)> Entries { get; } = new();

  #endregion

  #region Methods

  public IDisposable? BeginScope<TState>(TState state)
    where TState : notnull =>
    null;

  public bool IsEnabled(LogLevel logLevel) => true;

  public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
    Entries.Enqueue((eventId, logLevel, exception));

  #endregion
}
