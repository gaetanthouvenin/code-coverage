// ------------------------------------------------------------------------------------------------
// <copyright file="SignatureProbeBehavior.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

namespace CodeCoverage.Application.Tests.Messaging;

// A pass-through behavior that only compiles against the v3 signature of IPipelineBehavior
// (message, next, cancellationToken; next(message, cancellationToken); ValueTask).
internal sealed class SignatureProbeBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
  where TMessage : IMessage
{
  #region Properties

  public int Calls { get; private set; }

  #endregion

  #region Implementation of IPipelineBehavior<TMessage,TResponse>

  /// <inheritdoc />
  public ValueTask<TResponse> Handle(
    TMessage message,
    MessageHandlerDelegate<TMessage, TResponse> next,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(next);

    Calls++;
    return next(message, cancellationToken);
  }

  #endregion
}
