// ------------------------------------------------------------------------------------------------
// <copyright file="CommitAncestry.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Walks the first parents of a commit through the cache, filled from the forge by batches.</summary>
/// <param name="commits">The cache of the commit metadata.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="clock">The clock.</param>
/// <remarks>The parents always come from the forge's description of a commit, never from a webhook.</remarks>
internal sealed class CommitAncestry(IGitCommitRepository commits, IForgeGatewayProvider gateways, IClock clock)
{
  #region Fields

  /// <summary>How many commits one call to the forge brings back.</summary>
  internal const int PageSize = 50;

  #endregion

  #region Methods

  /// <summary>Gets the ancestors of a commit along its first parents, the nearest first, without the commit itself.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="repository">The address of the repository on its forge.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="maxDepth">The largest number of ancestors to return.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The ancestors; fewer than <paramref name="maxDepth" /> when the history ends or the forge does not have it.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> or <paramref name="sha" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDepth" /> is negative.</exception>
  public async Task<IReadOnlyList<GitCommit>> GetFirstParentChainAsync(
    long repositoryId,
    ForgeRepositoryRef repository,
    GitSha sha,
    int maxDepth,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

    var chain = new List<GitCommit>(maxDepth);

    if (maxDepth == 0)
    {
      return chain;
    }

    var current = await GetAsync(repositoryId, repository, sha, cancellationToken).ConfigureAwait(false);

    while (chain.Count < maxDepth && current is { ParentShas.Count: > 0 })
    {
      current = await GetAsync(repositoryId, repository, current.ParentShas[0], cancellationToken).ConfigureAwait(false);

      if (current is not null)
      {
        chain.Add(current);
      }
    }

    return chain;
  }

  /// <summary>Reads a commit from the cache, and fills the cache from the forge when it is missing.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="repository">The address of the repository on its forge.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The commit, or <see langword="null" /> when the forge does not have it.</returns>
  internal async Task<GitCommit?> GetAsync(long repositoryId, ForgeRepositoryRef repository, GitSha sha, CancellationToken cancellationToken)
  {
    var cached = await commits.FindAsync(repositoryId, sha, cancellationToken).ConfigureAwait(false);

    if (cached is not null)
    {
      return cached;
    }

    var page = await gateways.GetGateway(repository.Forge)
                             .ListCommitsAsync(repository, sha, PageSize, cancellationToken)
                             .ConfigureAwait(false);

    if (page.Count == 0)
    {
      return null;
    }

    await commits.AddMissingAsync(repositoryId, page, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return await commits.FindAsync(repositoryId, sha, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
