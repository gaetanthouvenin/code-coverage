// ------------------------------------------------------------------------------------------------
// <copyright file="IUserRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Ports;

/// <summary>The users who signed in through a forge.</summary>
/// <remarks>A global table without business data: it is read before a scope enters an organization.</remarks>
public interface IUserRepository
{
  #region Methods

  /// <summary>Finds a user by the identifier of the row.</summary>
  /// <param name="id">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The user, or <see langword="null" />.</returns>
  Task<AppUser?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Finds a user by the identifier the forge gives the account.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="externalId">The identifier of the account, whatever its case.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The user, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="externalId" /> is <see langword="null" />.</exception>
  Task<AppUser?> FindByExternalIdAsync(Forge forge, string externalId, CancellationToken cancellationToken);

  /// <summary>Adds a user, saved at once.</summary>
  /// <param name="user">The user.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns><see langword="true" /> when the user was added; <see langword="false" /> when the account already has a user.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="user" /> is <see langword="null" />.</exception>
  Task<bool> TryAddAsync(AppUser user, CancellationToken cancellationToken);

  /// <summary>Saves the changes of a user read in this scope.</summary>
  /// <param name="user">The user.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the user is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="user" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The user was not read in this scope.</exception>
  Task UpdateAsync(AppUser user, CancellationToken cancellationToken);

  #endregion
}
