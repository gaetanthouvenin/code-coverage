// ------------------------------------------------------------------------------------------------
// <copyright file="IGitCommitRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Ports;

/// <summary>The cache of the commit metadata read from the forges, for the organization of the scope.</summary>
/// <remarks>Every method sees the rows of that organization only, and throws when the scope has not entered one.</remarks>
public interface IGitCommitRepository
{
  #region Methods

  /// <summary>Finds the metadata of a commit, for reading only.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The metadata, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sha" /> is <see langword="null" />.</exception>
  Task<GitCommit?> FindAsync(long repositoryId, GitSha sha, CancellationToken cancellationToken);

  /// <summary>Adds the commits that are not cached yet, each saved at once.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="commits">The commits, as the forge reported them.</param>
  /// <param name="fetchedAt">When the forge was read.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the commits are cached.</returns>
  /// <remarks>
  ///   A row is never rewritten, and a commit added meanwhile by a concurrent call is left as it is. Refused as
  ///   not found (<c>Repository.NotFound</c>) when the repository is not one of the organization of the scope.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="commits" /> is <see langword="null" />.</exception>
  Task AddMissingAsync(
    long repositoryId,
    IReadOnlyCollection<ForgeCommitInfo> commits,
    DateTimeOffset fetchedAt,
    CancellationToken cancellationToken);

  #endregion
}
