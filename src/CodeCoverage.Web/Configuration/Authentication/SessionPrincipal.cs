// ------------------------------------------------------------------------------------------------
// <copyright file="SessionPrincipal.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.Cookies;

namespace CodeCoverage.Web.Configuration.Authentication;

/// <summary>
///   Builds and reads the principal of a session, the one the cookie carries: the internal identifier of the user and the
///   time of the sign-in, nothing else.
/// </summary>
/// <remarks>
///   No login, no organization, no token and no role ever reach the cookie (ARCHITECTURE §10bis.2): the login is read
///   from the database for each request and each circuit, and the organization always comes from the address.
/// </remarks>
internal static class SessionPrincipal
{
  #region Fields

  /// <summary>The type of the claim that holds the internal identifier of the user.</summary>
  public const string UserIdClaim = ClaimTypes.NameIdentifier;

  /// <summary>The type of the claim that holds when the session started, in Unix seconds.</summary>
  public const string SessionClaim = "cc:session";

  #endregion

  #region Methods

  /// <summary>Builds the principal of a new session.</summary>
  /// <param name="userId">The internal identifier of the user.</param>
  /// <param name="startedAt">When the session starts.</param>
  /// <returns>The principal, authenticated by the cookie scheme.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is not positive.</exception>
  public static ClaimsPrincipal Create(long userId, DateTimeOffset startedAt)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

    var identity = new ClaimsIdentity(
      [
        new Claim(UserIdClaim, userId.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        new Claim(SessionClaim, startedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
      ],
      CookieAuthenticationDefaults.AuthenticationScheme,
      UserIdClaim,
      ClaimTypes.Role
    );

    return new ClaimsPrincipal(identity);
  }

  /// <summary>Reads the internal identifier of the user from a principal.</summary>
  /// <param name="principal">The principal, or <see langword="null" />.</param>
  /// <param name="userId">The identifier read.</param>
  /// <returns><see langword="true" /> when the principal is authenticated and holds a positive identifier.</returns>
  public static bool TryReadUserId(ClaimsPrincipal? principal, out long userId)
  {
    userId = 0;

    return principal?.Identity is { IsAuthenticated: true }
           && principal.FindFirst(UserIdClaim)?.Value is { } value
           && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out userId)
           && userId > 0;
  }

  #endregion
}
