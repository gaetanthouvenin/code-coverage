// ------------------------------------------------------------------------------------------------
// <copyright file="SessionUsers.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Claims;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Web.Configuration.Authentication;

/// <summary>Finds the user a session belongs to, as long as the session may go on.</summary>
internal static class SessionUsers
{
  #region Methods

  /// <summary>Finds the user of a session.</summary>
  /// <param name="users">The repository of the users.</param>
  /// <param name="principal">The principal of the session, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The user, or <see langword="null" /> when the principal names nobody, when the user no longer exists, or when it
  ///   keeps no token GitHub could accept: a renewal GitHub refused clears them, and the session ends with it.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="users" /> is <see langword="null" />.</exception>
  public static async Task<AppUser?> FindAsync(IUserRepository users, ClaimsPrincipal? principal, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(users);

    if (!SessionPrincipal.TryReadUserId(principal, out var userId))
    {
      return null;
    }

    var user = await users.FindAsync(userId, cancellationToken).ConfigureAwait(false);

    return user is { AccessTokenProtected: not null } or { RefreshTokenProtected: not null } ? user : null;
  }

  #endregion
}
