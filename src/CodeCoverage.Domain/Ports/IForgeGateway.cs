// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeGateway.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;

namespace CodeCoverage.Domain.Ports;

/// <summary>The access to a forge, for what does not depend on one forge in particular.</summary>
/// <remarks>
///   A reading of something the forge does not have returns <see langword="null" /> or an empty list; a
///   forge that does not answer makes the reading throw. A tracked repository is always addressed by its
///   <see cref="ForgeRepositoryRef" />, so a renaming never breaks a reading.
/// </remarks>
public interface IForgeGateway
{
  #region Methods

  /// <summary>Finds the connection that serves a repository the instance does not track yet.</summary>
  /// <param name="owner">
  ///   The owner of the repository as written in addresses: a login, or with its levels on the forges that
  ///   have some (<c>org:project</c>, <c>group:sub</c>).
  /// </param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The connection and the identifiers needed to build a <see cref="ForgeRepositoryRef" />, or
  ///   <see langword="null" /> when no connection serves the repository.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="owner" /> or <paramref name="name" /> is <see langword="null" />.
  /// </exception>
  Task<ForgeConnectionInfo?> GetRepositoryConnectionAsync(string owner, string name, CancellationToken cancellationToken);

  /// <summary>Reads a repository.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repository, or <see langword="null" /> when the forge does not have it.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
  Task<ForgeRepositoryInfo?> GetRepositoryAsync(ForgeRepositoryRef repository, CancellationToken cancellationToken);

  /// <summary>Lists a commit and its ancestors, from the most recent, in one call to the forge.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="sha">The SHA of the commit to start from.</param>
  /// <param name="perPage">The maximum number of commits to return.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The commits, or an empty list when the forge does not have the commit.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="repository" /> or <paramref name="sha" /> is <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="perPage" /> is zero or negative.</exception>
  Task<IReadOnlyList<ForgeCommitInfo>> ListCommitsAsync(
    ForgeRepositoryRef repository,
    GitSha sha,
    int perPage,
    CancellationToken cancellationToken);

  /// <summary>Reads a commit.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The commit, or <see langword="null" /> when the forge does not have it.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="repository" /> or <paramref name="sha" /> is <see langword="null" />.
  /// </exception>
  Task<ForgeCommitInfo?> GetCommitAsync(ForgeRepositoryRef repository, GitSha sha, CancellationToken cancellationToken);

  /// <summary>Compares two commits.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="baseSha">The SHA of the base.</param>
  /// <param name="headSha">The SHA of the head.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The comparison, or <see langword="null" /> when the forge cannot compare these commits.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="repository" />, <paramref name="baseSha" /> or <paramref name="headSha" /> is
  ///   <see langword="null" />.
  /// </exception>
  Task<ForgeCompareResult?> CompareAsync(
    ForgeRepositoryRef repository,
    GitSha baseSha,
    GitSha headSha,
    CancellationToken cancellationToken);

  /// <summary>Reads a pull request.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The pull request, or <see langword="null" /> when the forge does not have it.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
  Task<ForgePullInfo?> GetPullAsync(ForgeRepositoryRef repository, int number, CancellationToken cancellationToken);

  /// <summary>Lists the files changed by a pull request, page after page.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The files, or <see langword="null" /> when the forge does not have the pull request.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="number" /> is zero or negative.</exception>
  Task<ForgePullFiles?> ListPullFilesAsync(ForgeRepositoryRef repository, int number, CancellationToken cancellationToken);

  /// <summary>Reads a tree.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="sha">The SHA of a commit, or of the tree of a directory.</param>
  /// <param name="recursive">Whether the subdirectories are read as well.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The tree, or <see langword="null" /> when the forge does not have it.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="repository" /> or <paramref name="sha" /> is <see langword="null" />.
  /// </exception>
  Task<ForgeTree?> GetTreeAsync(
    ForgeRepositoryRef repository,
    GitSha sha,
    bool recursive,
    CancellationToken cancellationToken);

  /// <summary>Publishes a commit status.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="sha">The commit that receives the status: the head of a pull request, never its merge commit.</param>
  /// <param name="status">The status.</param>
  /// <param name="targetUrl">The address the status links to, or <see langword="null" />.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>A task that completes when the forge took the status.</returns>
  /// <exception cref="ArgumentNullException">An argument other than <paramref name="targetUrl" /> is <see langword="null" />.</exception>
  Task CreateStatusAsync(ForgeRepositoryRef repository, GitSha sha, CommitStatus status, string? targetUrl, CancellationToken cancellationToken);

  /// <summary>Reads the text of a file at a commit.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="path">The path of the file.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The text of the file, or <see langword="null" /> when the forge does not have it.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="repository" />, <paramref name="path" /> or <paramref name="sha" /> is
  ///   <see langword="null" />.
  /// </exception>
  Task<string?> GetContentAsync(
    ForgeRepositoryRef repository,
    string path,
    GitSha sha,
    CancellationToken cancellationToken);

  #endregion
}
