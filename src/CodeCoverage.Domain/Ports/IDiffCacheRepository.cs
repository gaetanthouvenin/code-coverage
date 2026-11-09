// ------------------------------------------------------------------------------------------------
// <copyright file="IDiffCacheRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Ports;

/// <summary>The cache of the diffs read from the forges, immutable for a base and a head.</summary>
public interface IDiffCacheRepository
{
  #region Methods

  /// <summary>Finds a diff.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="baseSha">The SHA of the base.</param>
  /// <param name="headSha">The SHA of the head.</param>
  /// <param name="source">The API the diff comes from.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The diff, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="baseSha" /> or <paramref name="headSha" /> is <see langword="null" />.</exception>
  Task<DiffSet?> FindAsync(long repositoryId, GitSha baseSha, GitSha headSha, DiffSource source, CancellationToken cancellationToken);

  /// <summary>Adds a diff, saved at once, unless a concurrent call added it meanwhile.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="baseSha">The SHA of the base.</param>
  /// <param name="headSha">The SHA of the head.</param>
  /// <param name="diff">The diff.</param>
  /// <param name="fetchedAt">When the forge was read.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the diff is cached.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="baseSha" />, <paramref name="headSha" /> or <paramref name="diff" /> is <see langword="null" />.
  /// </exception>
  Task AddAsync(long repositoryId, GitSha baseSha, GitSha headSha, DiffSet diff, DateTimeOffset fetchedAt, CancellationToken cancellationToken);

  #endregion
}
