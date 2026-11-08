// ------------------------------------------------------------------------------------------------
// <copyright file="AuthorizationBehavior.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Behaviors;

/// <summary>
///   Pipeline behavior that refuses a message, before its validation and its handler, when the
///   current principal may not send it.
/// </summary>
/// <remarks>
///   <list type="bullet">
///     <item>the system principal (jobs, webhook processing) passes every check;</item>
///     <item>
///       an <see cref="ISystemOnly" /> message sent by anyone else is refused
///       (<see cref="ForbiddenException" />, <see cref="SecurityErrors.Forbidden" />);
///     </item>
///     <item>
///       an <see cref="IAdminOnly" /> message requires a signed-in user
///       (<see cref="UnauthorizedException" />, <see cref="SecurityErrors.Unauthenticated" />)
///       who administers the instance (<see cref="ForbiddenException" />,
///       <see cref="SecurityErrors.Forbidden" />);
///     </item>
///     <item>
///       a message without marker passes: its handler authenticates it when needed (an upload
///       checks its token).
///     </item>
///   </list>
///   <see cref="IRepositoryScoped" /> is not checked yet: the check comes with the repository
///   access service. The refusal is logged by <see cref="LoggingBehavior{TMessage,TResponse}" />,
///   which runs first.
/// </remarks>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <param name="currentUser">The principal the message is sent for.</param>
internal sealed class AuthorizationBehavior<TMessage, TResponse>(
  ICurrentUser currentUser) : IPipelineBehavior<TMessage, TResponse>
  where TMessage : IMessage
{
  #region Fields

  private static readonly bool IsAdminOnly = typeof(IAdminOnly).IsAssignableFrom(typeof(TMessage));

  private static readonly bool IsSystemOnly = typeof(ISystemOnly).IsAssignableFrom(typeof(TMessage));

  private static readonly string MessageName = typeof(TMessage).Name;

  #endregion

  #region Implementation of IPipelineBehavior<TMessage,TResponse>

  /// <inheritdoc />
  /// <exception cref="ForbiddenException">
  ///   The message is <see cref="ISystemOnly" /> and the sender is not the system, or it is
  ///   <see cref="IAdminOnly" /> and the signed-in user is not an administrator.
  /// </exception>
  /// <exception cref="UnauthorizedException">
  ///   The message is <see cref="IAdminOnly" /> and nobody is signed in.
  /// </exception>
  public ValueTask<TResponse> Handle(
    TMessage message,
    MessageHandlerDelegate<TMessage, TResponse> next,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(next);

    if (currentUser.IsSystem)
    {
      return next(message, cancellationToken);
    }

    if (IsSystemOnly)
    {
      throw new ForbiddenException(SecurityErrors.Forbidden, $"{MessageName} can only be sent by the system.");
    }

    if (IsAdminOnly)
    {
      if (!currentUser.IsAuthenticated)
      {
        throw new UnauthorizedException(SecurityErrors.Unauthenticated, $"{MessageName} requires a signed-in user.");
      }

      if (!currentUser.IsAdmin)
      {
        throw new ForbiddenException(SecurityErrors.Forbidden, $"{MessageName} requires an administrator.");
      }
    }

    return next(message, cancellationToken);
  }

  #endregion
}
