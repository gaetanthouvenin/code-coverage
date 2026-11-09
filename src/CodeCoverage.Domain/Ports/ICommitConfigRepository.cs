// ------------------------------------------------------------------------------------------------
// <copyright file="ICommitConfigRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Ports;

/// <summary>The cache of what was found as the configuration of the commits.</summary>
public interface ICommitConfigRepository
{
  #region Methods

  /// <summary>Finds what was found for a commit, for reading only.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The row, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sha" /> is <see langword="null" />.</exception>
  Task<CommitConfig?> FindAsync(long repositoryId, GitSha sha, CancellationToken cancellationToken);

  /// <summary>Adds a row, saved at once, unless a concurrent call added it meanwhile.</summary>
  /// <param name="config">The row.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the row is cached.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="config" /> is <see langword="null" />.</exception>
  Task AddAsync(CommitConfig config, CancellationToken cancellationToken);

  #endregion
}
