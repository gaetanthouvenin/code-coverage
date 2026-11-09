// ------------------------------------------------------------------------------------------------
// <copyright file="DiffProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Gives the diffs of the pull requests and of the comparisons, read once from the forge.</summary>
/// <param name="cache">The cache of the diffs.</param>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="clock">The clock.</param>
/// <remarks>The diff always comes from the forge, never from the git history of the CI agent.</remarks>
internal sealed class DiffProvider(IDiffCacheRepository cache, IForgeGatewayProvider gateways, IClock clock)
{
  #region Methods

  /// <summary>Gets the files of a pull request whose head is <paramref name="headSha" />.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="repository">The address of the repository on its forge.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <param name="baseSha">The SHA of its base, part of the key of the cache.</param>
  /// <param name="headSha">The SHA of its head, part of the key of the cache.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The diff, or <see langword="null" /> when the forge does not have the pull request.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="repository" />, <paramref name="baseSha" /> or <paramref name="headSha" /> is <see langword="null" />.
  /// </exception>
  public async Task<DiffSet?> GetPullDiffAsync(
    long repositoryId,
    ForgeRepositoryRef repository,
    int number,
    GitSha baseSha,
    GitSha headSha,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(baseSha);
    ArgumentNullException.ThrowIfNull(headSha);

    var cached = await cache.FindAsync(repositoryId, baseSha, headSha, DiffSource.PullFiles, cancellationToken).ConfigureAwait(false);

    if (cached is not null)
    {
      return cached;
    }

    var read = await gateways.GetGateway(repository.Forge)
                             .ListPullFilesAsync(repository, number, cancellationToken)
                             .ConfigureAwait(false);

    return read is null
      ? null
      : await StoreAsync(repositoryId, baseSha, headSha, read.Files, read.Truncated, DiffSource.PullFiles, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Gets the files changed between two commits.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="repository">The address of the repository on its forge.</param>
  /// <param name="baseSha">The SHA of the base.</param>
  /// <param name="headSha">The SHA of the head.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The diff, or <see langword="null" /> when the forge cannot compare these commits.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="repository" />, <paramref name="baseSha" /> or <paramref name="headSha" /> is <see langword="null" />.
  /// </exception>
  public async Task<DiffSet?> GetCompareDiffAsync(
    long repositoryId,
    ForgeRepositoryRef repository,
    GitSha baseSha,
    GitSha headSha,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(baseSha);
    ArgumentNullException.ThrowIfNull(headSha);

    var cached = await cache.FindAsync(repositoryId, baseSha, headSha, DiffSource.Compare, cancellationToken).ConfigureAwait(false);

    if (cached is not null)
    {
      return cached;
    }

    var read = await gateways.GetGateway(repository.Forge)
                             .CompareAsync(repository, baseSha, headSha, cancellationToken)
                             .ConfigureAwait(false);

    return read is null
      ? null
      : await StoreAsync(repositoryId, baseSha, headSha, read.Files, read.Truncated, DiffSource.Compare, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Reads the status a forge reports.</summary>
  /// <param name="status">The status on the forge.</param>
  /// <returns>The status of the diff.</returns>
  private static FileDiffStatus ToStatus(ForgeChangedFileStatus status) =>
    status switch
    {
      ForgeChangedFileStatus.Added => FileDiffStatus.Added,
      ForgeChangedFileStatus.Removed => FileDiffStatus.Removed,
      ForgeChangedFileStatus.Renamed => FileDiffStatus.Renamed,
      ForgeChangedFileStatus.Copied => FileDiffStatus.Copied,
      ForgeChangedFileStatus.Unchanged => FileDiffStatus.Unchanged,
      var _ => FileDiffStatus.Modified,
    };

  /// <summary>Builds a diff and caches it.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="baseSha">The SHA of the base.</param>
  /// <param name="headSha">The SHA of the head.</param>
  /// <param name="files">The files the forge returned.</param>
  /// <param name="truncated">Whether the forge left out some files.</param>
  /// <param name="source">The API the files come from.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The diff.</returns>
  private async Task<DiffSet> StoreAsync(
    long repositoryId,
    GitSha baseSha,
    GitSha headSha,
    IReadOnlyList<ForgeChangedFile> files,
    bool truncated,
    DiffSource source,
    CancellationToken cancellationToken)
  {
    var diff = new DiffSet([.. files.Select(static f => FileDiff.Create(f.Path, f.PreviousPath, ToStatus(f.Status), f.Patch))], truncated, source);

    await cache.AddAsync(repositoryId, baseSha, headSha, diff, clock.UtcNow, cancellationToken).ConfigureAwait(false);

    return diff;
  }

  #endregion
}
