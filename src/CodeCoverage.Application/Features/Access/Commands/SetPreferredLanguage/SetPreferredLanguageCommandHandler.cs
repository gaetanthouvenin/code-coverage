// ------------------------------------------------------------------------------------------------
// <copyright file="SetPreferredLanguageCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Access.Commands.SetPreferredLanguage;

/// <summary>Handler for the <see cref="SetPreferredLanguageCommand" /> command.</summary>
/// <param name="currentUser">The principal of the scope.</param>
/// <param name="users">The repository of the users.</param>
/// <remarks>The same language again changes nothing and saves nothing.</remarks>
internal sealed class SetPreferredLanguageCommandHandler(ICurrentUser currentUser, IUserRepository users) : ICommandHandler<SetPreferredLanguageCommand>
{
  #region Implementation of ICommandHandler<SetPreferredLanguageCommand>

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">
  ///   The principal has no user row, or its row no longer exists (<see cref="SecurityErrors.Unauthenticated" />).
  /// </exception>
  public async ValueTask<Unit> Handle(SetPreferredLanguageCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    // The system passes the gate of IUserScoped, yet it is no user.
    var user = currentUser.UserId is { } userId ? await users.FindAsync(userId, cancellationToken).ConfigureAwait(false) : null;

    UnauthorizedException.ThrowIfSignedOut(user);

    if (string.Equals(user.PreferredLanguage, command.Language, StringComparison.Ordinal))
    {
      return Unit.Value;
    }

    user.SetPreferredLanguage(command.Language);
    await users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
