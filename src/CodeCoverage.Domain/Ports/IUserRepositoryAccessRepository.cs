// ------------------------------------------------------------------------------------------------
// <copyright file="IUserRepositoryAccessRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Ports;

/// <summary>The cache of what the users may do on the repositories of the organization of the scope.</summary>
public interface IUserRepositoryAccessRepository
{
  #region Methods

  /// <summary>Finds the access of a user to a repository.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="repositoryExternalId">The identifier the forge gives the repository, whatever its case.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The access, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repositoryExternalId" /> is <see langword="null" />.</exception>
  Task<RepositoryAccess?> FindAsync(long userId, string repositoryExternalId, CancellationToken cancellationToken);

  /// <summary>Lists the accesses of a user in the organization of the scope.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The accesses, by repository.</returns>
  Task<IReadOnlyList<RepositoryAccess>> ListOfUserAsync(long userId, CancellationToken cancellationToken);

  /// <summary>Records what the forge reports for a user and a repository, saved at once.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="repositoryExternalId">The identifier the forge gives the repository.</param>
  /// <param name="permission">The access level.</param>
  /// <param name="at">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The access.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repositoryExternalId" /> is <see langword="null" />.</exception>
  Task<RepositoryAccess> UpsertAsync(long userId, string repositoryExternalId, RepositoryPermission permission, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>Removes the access of a user to a repository, when there is one.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="repositoryExternalId">The identifier the forge gives the repository, whatever its case.</param>
  /// <param name="cancellationToken">A token to cancel the deletion.</param>
  /// <returns>A task that completes when the access is removed.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repositoryExternalId" /> is <see langword="null" />.</exception>
  Task RemoveAsync(long userId, string repositoryExternalId, CancellationToken cancellationToken);

  #endregion
}
