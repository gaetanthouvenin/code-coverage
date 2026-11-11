// ------------------------------------------------------------------------------------------------
// <copyright file="IGitHubUserTokenProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>Gives the token GitHub issued for a user, renewed when it is about to expire.</summary>
/// <remarks>
///   <para>
///     The tokens of a user are stored on its row, protected. A user token expires after a few hours and is
///     renewed with a refresh token that GitHub accepts once: the renewal replaces both.
///   </para>
///   <para>
///     Refused as unauthenticated (<c>Access.Unauthenticated</c>) when the user has no token left to use: it never
///     signed in, its refresh token expired, or GitHub refused it. The stored tokens are then cleared, and the
///     user signs in again.
///   </para>
/// </remarks>
public interface IGitHubUserTokenProvider
{
  #region Methods

  /// <summary>Gives a user token that GitHub accepts now.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The token, which the caller must not keep.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is zero or negative.</exception>
  Task<string> GetTokenAsync(long userId, CancellationToken cancellationToken);

  #endregion
}
