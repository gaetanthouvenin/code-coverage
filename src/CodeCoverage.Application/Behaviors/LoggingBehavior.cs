// ------------------------------------------------------------------------------------------------
// <copyright file="LoggingBehavior.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics;

using Mediator;

using Microsoft.Extensions.Logging;

using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;

namespace CodeCoverage.Application.Behaviors;

/// <summary>
///   Pipeline behavior that logs the message name, execution time, and outcome.
/// </summary>
/// <remarks>
///   <para>
///     Queries are logged at <c>Debug</c> (screens and the API read often); commands, which change
///     data, stay at <c>Information</c>. A refusal (<see cref="ApplicationException" />) is an
///     answer, logged at <c>Warning</c> with its error code; any other exception at <c>Error</c>.
///   </para>
///   <para>
///     Only the name of the message type is logged: the message itself is never serialized, so
///     neither a token nor an upload stream it carries can leak into the logs.
///   </para>
/// </remarks>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <param name="logger">The logger.</param>
internal sealed class LoggingBehavior<TMessage, TResponse>(
  ILogger<LoggingBehavior<TMessage, TResponse>> logger) : IPipelineBehavior<TMessage, TResponse>
  where TMessage : IMessage
{
  #region Fields

  // A query reads often: Debug keeps it out of the logs in production.
  private static readonly LogLevel Level = typeof(IBaseQuery).IsAssignableFrom(typeof(TMessage))
                                             ? LogLevel.Debug
                                             : LogLevel.Information;

  private static readonly string MessageName = typeof(TMessage).Name;

  #endregion

  #region Implementation of IPipelineBehavior<TMessage,TResponse>

  /// <inheritdoc />
  public async ValueTask<TResponse> Handle(
    TMessage message,
    MessageHandlerDelegate<TMessage, TResponse> next,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(next);

    logger.Log(Level, "Handling {MessageName}", MessageName);
    var stopwatch = Stopwatch.StartNew();

    try
    {
      var response = await next(message, cancellationToken).ConfigureAwait(false);
      stopwatch.Stop();

      logger.Log(
        Level,
        "Handled {MessageName} in {ElapsedMilliseconds}ms",
        MessageName,
        stopwatch.ElapsedMilliseconds
      );

      return response;
    }
    catch (ApplicationException ex)
    {
      stopwatch.Stop();

      // A refusal is an answer, not a failure.
      logger.LogWarning(
        "Refused {MessageName} after {ElapsedMilliseconds}ms: {ErrorCode}",
        MessageName,
        stopwatch.ElapsedMilliseconds,
        ex.ErrorCode
      );

      throw;
    }
    catch (Exception ex)
    {
      stopwatch.Stop();

      logger.LogError(
        ex,
        "Failed {MessageName} after {ElapsedMilliseconds}ms",
        MessageName,
        stopwatch.ElapsedMilliseconds
      );

      throw;
    }
  }

  #endregion
}
