// ------------------------------------------------------------------------------------------------
// <copyright file="MergeCommitDetector.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Services;

/// <summary>Finds the head of a pull request behind a synthetic merge commit, whatever the uploader sent (§1.4-2).</summary>
/// <param name="gateways">The gateways of the forges.</param>
/// <param name="ancestry">The commits read from the forge, through their cache.</param>
internal sealed class MergeCommitDetector(IForgeGatewayProvider gateways, CommitAncestry ancestry)
{
  #region Methods

  /// <summary>Tells which commit an upload should be attached to.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="repository">The address of the repository on its forge.</param>
  /// <param name="sha">The SHA the upload named.</param>
  /// <param name="pullNumber">The number of its pull request.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The SHA of the head of the pull request, or <see langword="null" /> when the upload is on the right commit.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> or <paramref name="sha" /> is <see langword="null" />.</exception>
  public async Task<GitSha?> DetectAsync(
    long repositoryId,
    ForgeRepositoryRef repository,
    GitSha sha,
    int pullNumber,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(sha);

    var pull = await gateways.GetGateway(repository.Forge).GetPullAsync(repository, pullNumber, cancellationToken).ConfigureAwait(false);

    if (pull is null || pull.HeadSha == sha)
    {
      return null;
    }

    if (pull.MergeCommitSha == sha)
    {
      return pull.HeadSha;
    }

    var commit = await ancestry.GetAsync(repositoryId, repository, sha, cancellationToken).ConfigureAwait(false);

    return commit is { ParentShas.Count: 2 } && commit.ParentShas[1] == pull.HeadSha ? pull.HeadSha : null;
  }

  #endregion
}
