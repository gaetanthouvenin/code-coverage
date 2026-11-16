// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeUserGateway.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Domain.Ports;

/// <summary>Calls a forge in the name of a user, with the token the forge issued for that user.</summary>
/// <remarks>
///   Neutral (ADR-014): each forge implements it under its <see cref="Organizations.Forge" />, which
///   <see cref="IForgeGatewayProvider.GetUserGateway" /> resolves. It serves the sign-in and the access of a user to
///   repositories. The token is given by the caller, which gets it from the sign-in itself or from the stored tokens of the
///   user; it is never kept here.
/// </remarks>
public interface IForgeUserGateway
{
  #region Methods

  /// <summary>Reads the account a user token belongs to.</summary>
  /// <param name="accessToken">The user token.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The account.</returns>
  /// <exception cref="ArgumentException"><paramref name="accessToken" /> is <see langword="null" />, empty or white space.</exception>
  Task<ForgeUser> GetUserAsync(string accessToken, CancellationToken cancellationToken);

  /// <summary>Lists the accounts the user reaches through the access the forge gives this instance, read to the last page.</summary>
  /// <param name="accessToken">The user token.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The accounts, suspended ones included, whatever the state of their organization in this instance.</returns>
  /// <exception cref="ArgumentException"><paramref name="accessToken" /> is <see langword="null" />, empty or white space.</exception>
  Task<IReadOnlyList<ForgeUserAccount>> ListAccountsAsync(string accessToken, CancellationToken cancellationToken);

  /// <summary>Lists the repositories of an account that the user reaches, with the permission of the user, read to the last page.</summary>
  /// <param name="accessToken">The user token.</param>
  /// <param name="account">The account, as <see cref="ListAccountsAsync" /> reports it.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repositories; empty when the user cannot reach the account.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="account" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">
  ///   <paramref name="accessToken" /> is <see langword="null" />, empty or white space, or <paramref name="account" /> was
  ///   not reported by this forge.
  /// </exception>
  Task<IReadOnlyList<ForgeRepositoryPermission>> ListRepositoryPermissionsAsync(
    string accessToken,
    ForgeUserAccount account,
    CancellationToken cancellationToken);

  #endregion
}
