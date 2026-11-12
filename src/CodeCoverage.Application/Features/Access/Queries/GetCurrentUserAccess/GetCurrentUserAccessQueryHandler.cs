// ------------------------------------------------------------------------------------------------
// <copyright file="GetCurrentUserAccessQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access.Queries.GetCurrentUserAccess;

/// <summary>Handler for the <see cref="GetCurrentUserAccessQuery" /> query.</summary>
/// <param name="currentUser">The user of the request.</param>
/// <param name="users">The users.</param>
internal sealed class GetCurrentUserAccessQueryHandler(ICurrentUser currentUser, IUserRepository users) : IQueryHandler<GetCurrentUserAccessQuery, UserAccess>
{
  #region Implementation of IQueryHandler<GetCurrentUserAccessQuery,UserAccess>

  /// <inheritdoc />
  public async ValueTask<UserAccess> Handle(GetCurrentUserAccessQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    // The system passes the gate of IUserScoped, yet it is no user.
    var user = currentUser.UserId is { } userId ? await users.FindAsync(userId, cancellationToken).ConfigureAwait(false) : null;

    UnauthorizedException.ThrowIfSignedOut(user);

    // A token of an organization never shows the instance (M3.05).
    return new UserAccess(user, currentUser.IsAdmin && currentUser.TokenOrganizationId is null);
  }

  #endregion
}
