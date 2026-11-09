// ------------------------------------------------------------------------------------------------
// <copyright file="WarningRecorder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Keeps the text of the warnings written to a logger.
public sealed class WarningRecorder<T> : ILogger<T>
{
  #region Properties

  public List<string> Warnings { get; } = [];

  #endregion

  #region Implementation of ILogger

  public IDisposable? BeginScope<TState>(TState state)
    where TState : notnull => null;

  public bool IsEnabled(LogLevel logLevel) => true;

  public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
  {
    if (logLevel is LogLevel.Warning)
    {
      Warnings.Add(formatter(state, exception));
    }
  }

  #endregion
}
