// ------------------------------------------------------------------------------------------------
// <copyright file="ReservedLogins.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Frozen;

namespace CodeCoverage.Domain.Organizations;

/// <summary>The logins that cannot be admitted, because they are the first segment of a route of the product.</summary>
/// <remarks>
///   An owner shares the first segment of an address with the literal routes of both hosts: an account
///   named after one of them could shadow it. Routes that start with an underscore (<c>_blazor</c>,
///   <c>_framework</c>) are absent, because such a segment is not a well-formed login.
/// </remarks>
public static class ReservedLogins
{
  #region Fields

  /// <summary>The reserved logins, compared whatever their case.</summary>
  private static readonly FrozenSet<string> Logins = FrozenSet.ToFrozenSet(
    [
      "access-denied",
      "admin",
      "api",
      "error",
      "gh",
      "github",
      "health",
      "login",
      "logout",
      "not-found",
      "openapi",
      "orgs",
      "scalar",
      "setup",
      "signin-github",
      "swagger",
      "upload",
    ],
    StringComparer.OrdinalIgnoreCase
  );

  #endregion

  #region Methods

  /// <summary>Tells whether a login is reserved.</summary>
  /// <param name="login">The login, in any case.</param>
  /// <returns><see langword="true" /> when the login is the first segment of a route; otherwise <see langword="false" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="login" /> is <see langword="null" />.</exception>
  public static bool IsReserved(string login)
  {
    ArgumentNullException.ThrowIfNull(login);

    return Logins.Contains(login);
  }

  #endregion
}
