// ------------------------------------------------------------------------------------------------
// <copyright file="IBranchRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Ports;

/// <summary>The branches of the repositories of the organization of the scope.</summary>
/// <remarks>
///   <para>Every method sees the rows of that organization only, and throws when the scope has not entered one.</para>
///   <para>
///     An instance returned by <see cref="FindAsync" /> stays attached to the scope: change it, then give it
///     to <see cref="UpdateAsync" />. An instance from <see cref="ListAsync" /> is for reading only.
///   </para>
/// </remarks>
public interface IBranchRepository
{
  #region Methods

  /// <summary>Finds a branch of a repository.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="name">The name of the branch.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The branch, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="name" /> is <see langword="null" />.</exception>
  Task<Branch?> FindAsync(long repositoryId, string name, CancellationToken cancellationToken);

  /// <summary>Lists the branches of a repository, for reading only.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The branches, by name.</returns>
  Task<IReadOnlyList<Branch>> ListAsync(long repositoryId, CancellationToken cancellationToken);

  /// <summary>Adds a branch, saved at once.</summary>
  /// <param name="branch">The branch.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the branch is saved.</returns>
  /// <remarks>
  ///   Refused as not found (<c>Repository.NotFound</c>) when the repository is not one of the organization of
  ///   the scope, and as a conflict (<c>General.ConcurrentChange</c>) when a concurrent save added the same
  ///   branch: reading again finds its row.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="branch" /> is <see langword="null" />.</exception>
  Task AddAsync(Branch branch, CancellationToken cancellationToken);

  /// <summary>Saves the changes of a branch returned by <see cref="FindAsync" /> in this scope, at once.</summary>
  /// <param name="branch">The branch.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the branch is saved.</returns>
  /// <remarks>Refused as a conflict (<c>General.ConcurrentChange</c>) when the row changed or disappeared since it was read.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="branch" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The instance was not read in this scope.</exception>
  Task UpdateAsync(Branch branch, CancellationToken cancellationToken);

  /// <summary>Removes a branch, saved at once; a branch that does not exist leaves nothing to do.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="name">The name of the branch.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the branch is removed.</returns>
  /// <remarks>Refused as a conflict (<c>General.ConcurrentChange</c>) when the row disappeared while it was being removed.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="name" /> is <see langword="null" />.</exception>
  Task RemoveAsync(long repositoryId, string name, CancellationToken cancellationToken);

  #endregion
}
