// ------------------------------------------------------------------------------------------------
// <copyright file="IRepositoryTreeRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Ports;

/// <summary>The cache of the file trees read from the forges, immutable for a SHA.</summary>
public interface IRepositoryTreeRepository
{
  #region Methods

  /// <summary>Finds the files of a repository at a SHA.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="sha">The SHA that was read.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The tree, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sha" /> is <see langword="null" />.</exception>
  Task<ForgeTree?> FindAsync(long repositoryId, GitSha sha, CancellationToken cancellationToken);

  /// <summary>Adds a tree, saved at once, unless a concurrent call added it meanwhile.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="tree">The tree, its files only.</param>
  /// <param name="fetchedAt">When the forge was read.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the tree is cached.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="tree" /> is <see langword="null" />.</exception>
  Task AddAsync(long repositoryId, ForgeTree tree, DateTimeOffset fetchedAt, CancellationToken cancellationToken);

  #endregion
}
