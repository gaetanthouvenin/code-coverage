// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryTreeProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Gives the files of a repository at a SHA, read once from the forge.</summary>
/// <param name="trees">The cache of the trees.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="clock">The clock.</param>
internal sealed class RepositoryTreeProvider(IRepositoryTreeRepository trees, IForgeGatewayProvider gateways, IClock clock)
{
  #region Methods

  /// <summary>Gets the files of a repository at a SHA.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="repository">The address of the repository on its forge.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The tree, its files only, or <see langword="null" /> when the forge does not have it.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> or <paramref name="sha" /> is <see langword="null" />.</exception>
  public async Task<ForgeTree?> GetAsync(long repositoryId, ForgeRepositoryRef repository, GitSha sha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);

    var cached = await trees.FindAsync(repositoryId, sha, cancellationToken).ConfigureAwait(false);

    if (cached is not null)
    {
      return cached;
    }

    var read = await gateways.GetGateway(repository.Forge)
                             .GetTreeAsync(repository, sha, recursive: true, cancellationToken)
                             .ConfigureAwait(false);

    if (read is null)
    {
      return null;
    }

    var files = read with { Entries = [.. read.Entries.Where(static e => !e.IsDirectory)] };

    await trees.AddAsync(repositoryId, files, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return files;
  }

  #endregion
}
