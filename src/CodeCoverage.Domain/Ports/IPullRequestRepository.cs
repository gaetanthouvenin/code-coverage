// ------------------------------------------------------------------------------------------------
// <copyright file="IPullRequestRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.PullRequests;

namespace CodeCoverage.Domain.Ports;

/// <summary>The pull requests of the tracked repositories of the organization of the scope.</summary>
public interface IPullRequestRepository
{
  #region Methods

  /// <summary>Finds the open pull request whose head is a commit, which can then be updated.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="headSha">The SHA of its head.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The pull request with the highest number when several share the head, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="headSha" /> is <see langword="null" />.</exception>
  Task<PullRequest?> FindOpenByHeadShaAsync(long repositoryId, GitSha headSha, CancellationToken cancellationToken);

  /// <summary>Lists the open pull requests that forked from a commit.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="forkPointSha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The pull requests, for reading only.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="forkPointSha" /> is <see langword="null" />.</exception>
  Task<IReadOnlyList<PullRequest>> ListOpenByForkPointAsync(long repositoryId, GitSha forkPointSha, CancellationToken cancellationToken);

  /// <summary>Adds a pull request or applies to it what its forge reports, saved at once.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="pull">What the forge reports.</param>
  /// <param name="at">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The pull request, which can then be updated.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="pull" /> is <see langword="null" />.</exception>
  Task<PullRequest> UpsertAsync(long repositoryId, ForgePullInfo pull, DateTimeOffset at, CancellationToken cancellationToken);

  #endregion
}
