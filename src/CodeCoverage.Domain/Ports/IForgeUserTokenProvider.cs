// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeUserTokenProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>Gives the token a forge issued for a user, renewed when it is about to expire.</summary>
/// <remarks>
///   <para>
///     Neutral (ADR-014): each forge implements it under its <see cref="Organizations.Forge" />, which
///     <see cref="IForgeGatewayProvider.GetUserTokenProvider" /> resolves; the caller asks the provider of the forge of
///     the user.
///   </para>
///   <para>
///     The tokens of a user are stored on its row, protected. A user token expires after a few hours and is renewed
///     with a refresh token that the forge may accept only once: the renewal replaces both.
///   </para>
///   <para>
///     Refused as unauthenticated (<c>Access.Unauthenticated</c>) when the user has no token left to use: it never
///     signed in, its refresh token expired, or the forge refused it. The stored tokens are then cleared, and the
///     user signs in again.
///   </para>
/// </remarks>
public interface IForgeUserTokenProvider
{
  #region Methods

  /// <summary>Gives a user token that the forge accepts now.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The token, which the caller must not keep.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId" /> is zero or negative.</exception>
  Task<string> GetTokenAsync(long userId, CancellationToken cancellationToken);

  #endregion
}
