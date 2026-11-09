// ------------------------------------------------------------------------------------------------
// <copyright file="IRepositoryRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Ports;

/// <summary>The repositories tracked by the organization of the scope.</summary>
/// <remarks>
///   <para>Every method sees the rows of that organization only, and throws when the scope has not entered one.</para>
///   <para>
///     An instance returned by a <c>Find</c> method stays attached to the scope: change it, then give it to
///     <see cref="UpdateAsync" />. An instance from <see cref="ListAsync" /> is for reading only.
///   </para>
/// </remarks>
public interface IRepositoryRepository
{
  #region Methods

  /// <summary>Finds a repository by its identifier.</summary>
  /// <param name="id">The identifier of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repository, or <see langword="null" />.</returns>
  Task<TrackedRepository?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Finds a repository by its namespace and its name, whatever their case.</summary>
  /// <param name="slug">The name of the repository; its owner is not compared.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repository, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="slug" /> is <see langword="null" />.</exception>
  Task<TrackedRepository?> FindBySlugAsync(RepositorySlug slug, CancellationToken cancellationToken);

  /// <summary>Finds a repository by the identifier its forge gives it, whatever its case.</summary>
  /// <param name="externalId">The identifier given by the forge.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repository, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="externalId" /> is <see langword="null" />.</exception>
  Task<TrackedRepository?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken);

  /// <summary>Lists the tracked repositories, for reading only.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repositories, by namespace and name.</returns>
  Task<IReadOnlyList<TrackedRepository>> ListAsync(CancellationToken cancellationToken);

  /// <summary>Adds a repository, saved at once.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the repository is saved.</returns>
  /// <remarks>
  ///   Refused as a conflict (<c>General.ConcurrentChange</c>) when a concurrent save added the same name or
  ///   external identifier: reading again finds its row.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
  Task AddAsync(TrackedRepository repository, CancellationToken cancellationToken);

  /// <summary>Saves the changes of a repository returned by a <c>Find</c> method of this scope, at once.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the repository is saved.</returns>
  /// <remarks>Refused as a conflict (<c>General.ConcurrentChange</c>) when the row changed or disappeared since it was read.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The instance was not read in this scope.</exception>
  Task UpdateAsync(TrackedRepository repository, CancellationToken cancellationToken);

  #endregion
}
