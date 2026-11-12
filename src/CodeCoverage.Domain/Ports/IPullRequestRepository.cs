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

  /// <summary>Finds a pull request of a repository by its number, which can then be updated.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The pull request, or <see langword="null" />.</returns>
  Task<PullRequest?> FindByNumberAsync(long repositoryId, int number, CancellationToken cancellationToken);

  /// <summary>Lists one page of the pull requests of a repository, the most recently updated first.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="state">The state the pull requests are in, or <see langword="null" /> for every state.</param>
  /// <param name="page">The number of the page, from 1.</param>
  /// <param name="pageSize">The number of pull requests of a page.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The page, for reading only; the pull requests updated at the same time come by decreasing number.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="page" /> or <paramref name="pageSize" /> is zero or negative.</exception>
  Task<PullRequestPage> ListPageAsync(long repositoryId, PullRequestState? state, int page, int pageSize, CancellationToken cancellationToken);

  /// <summary>Lists the open pull requests that forked from a commit and are not compared to it yet.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="forkPointSha">The SHA of the commit.</param>
  /// <param name="limit">The number of pull requests to list, at most.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The pull requests, the most recently updated first, for reading only; those whose head is the commit, or that
  ///   are already compared to it, are left out before the limit.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="forkPointSha" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit" /> is zero or negative.</exception>
  Task<IReadOnlyList<PullRequest>> ListOpenByForkPointAsync(long repositoryId, GitSha forkPointSha, int limit, CancellationToken cancellationToken);

  /// <summary>Adds a pull request or applies to it what its forge reports, saved at once.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="pull">What the forge reports.</param>
  /// <param name="at">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The pull request, which can then be updated.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="pull" /> is <see langword="null" />.</exception>
  Task<PullRequest> UpsertAsync(long repositoryId, ForgePullInfo pull, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>Saves the changes of a pull request returned by this repository in this scope, at once.</summary>
  /// <param name="pullRequest">The pull request.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the pull request is saved.</returns>
  /// <remarks>
  ///   Refused as a conflict (<c>General.ConcurrentChange</c>) when the row changed or disappeared since it was read:
  ///   nothing of the change is kept.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="pullRequest" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The instance was not read in this scope.</exception>
  Task UpdateAsync(PullRequest pullRequest, CancellationToken cancellationToken);

  #endregion
}
