// ------------------------------------------------------------------------------------------------
// <copyright file="CommitLocator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Services;

/// <summary>Finds the repository and the commit a read of the API or of a page addresses, in the organization of the scope.</summary>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="commits">The commits of the organization.</param>
/// <remarks>The gate has already checked that the user may read the repository: this only resolves it.</remarks>
internal sealed class CommitLocator(IRepositoryRepository repositories, ICommitRepository commits)
{
  #region Methods

  /// <summary>Finds a repository by its address.</summary>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repository.</returns>
  /// <exception cref="NotFoundException">The repository is not tracked (<see cref="RepositoryErrors.NotFound" />).</exception>
  public async Task<TrackedRepository> FindRepositoryAsync(string owner, string name, CancellationToken cancellationToken)
  {
    var repository = RepositorySlug.TryFromAddress(owner, name, out var slug)
                       ? await repositories.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false)
                       : null;

    NotFoundException.ThrowIfRepositoryNotFound(repository);

    return repository;
  }

  /// <summary>Finds a commit of a repository by its address and its SHA.</summary>
  /// <param name="owner">The owner segment of the address.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The commit.</returns>
  /// <exception cref="NotFoundException">
  ///   The repository is not tracked (<see cref="RepositoryErrors.NotFound" />), or it has no such commit
  ///   (<see cref="CommitErrors.NotFound" />).
  /// </exception>
  public async Task<Commit> FindCommitAsync(string owner, string name, GitSha sha, CancellationToken cancellationToken)
  {
    var repository = await FindRepositoryAsync(owner, name, cancellationToken).ConfigureAwait(false);
    var commit = await commits.FindByShaAsync(repository.Id, sha, cancellationToken).ConfigureAwait(false);

    NotFoundException.ThrowIfCommitNotFound(commit);

    return commit;
  }

  #endregion
}
