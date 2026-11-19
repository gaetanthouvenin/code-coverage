// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeDirectoryGateway.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Domain.Ports;

/// <summary>Reads, through the connection of an organization, the account, its members and the repositories its forge serves.</summary>
/// <remarks>
///   Each forge implements it under its <see cref="Organizations.Forge" />, which
///   <see cref="IForgeGatewayProvider.GetDirectory" /> resolves: the synchronization of the repositories of a
///   connection is the same for every forge.
/// </remarks>
public interface IForgeDirectoryGateway
{
  #region Methods

  /// <summary>Reads the account a connection serves.</summary>
  /// <param name="connection">The connection.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The account, or <see langword="null" /> when the forge no longer knows the connection.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connection" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connection" /> is not a connection of the forge of the gateway.</exception>
  Task<ForgeAccountInfo?> GetAccountAsync(ForgeConnectionRef connection, CancellationToken cancellationToken);

  /// <summary>Reads, through a connection, what the forge says of a user in the account the connection serves.</summary>
  /// <param name="connection">The connection.</param>
  /// <param name="account">The account the connection serves, as it is known here.</param>
  /// <param name="user">The user.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The membership, or <see langword="null" /> when the user is not a member of the account: an outside collaborator
  ///   of one of its repositories, for instance.
  /// </returns>
  /// <remarks>
  ///   Read with the credential of the connection, not with the token of the user: the role does not depend on what the
  ///   user let this instance see. A forge that refuses to tell the membership in this account raises the
  ///   <c>ForbiddenException</c> of the Application, which the caller reads as no membership at all.
  /// </remarks>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connection" /> is not a connection of the forge of the gateway.</exception>
  Task<ForgeMembership?> GetMembershipAsync(
    ForgeConnectionRef connection,
    ForgeAccountInfo account,
    ForgeUser user,
    CancellationToken cancellationToken);

  /// <summary>Lists every repository a connection serves, to its last page.</summary>
  /// <param name="connection">The connection.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repositories: a listing that cannot be read to its end fails rather than being partial.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connection" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connection" /> is not a connection of the forge of the gateway.</exception>
  Task<IReadOnlyList<ForgeRepositoryInfo>> ListRepositoriesAsync(ForgeConnectionRef connection, CancellationToken cancellationToken);

  #endregion
}
