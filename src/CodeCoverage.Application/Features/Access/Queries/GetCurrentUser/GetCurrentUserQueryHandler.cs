// ------------------------------------------------------------------------------------------------
// <copyright file="GetCurrentUserQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access.Queries.GetCurrentUser;

/// <summary>Handler for the <see cref="GetCurrentUserQuery" /> query.</summary>
/// <param name="currentUser">The principal of the scope.</param>
/// <param name="users">The repository of the users.</param>
internal sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser, IUserRepository users) : IQueryHandler<GetCurrentUserQuery, AppUser>
{
  #region Implementation of IQueryHandler<GetCurrentUserQuery,AppUser>

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">
  ///   The principal has no user row, or its row no longer exists (<see cref="SecurityErrors.Unauthenticated" />).
  /// </exception>
  public async ValueTask<AppUser> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    // The system passes the gate of IUserScoped, yet it is no user.
    var user = currentUser.UserId is { } userId ? await users.FindAsync(userId, cancellationToken).ConfigureAwait(false) : null;

    UnauthorizedException.ThrowIfSignedOut(user);

    return user;
  }

  #endregion
}
