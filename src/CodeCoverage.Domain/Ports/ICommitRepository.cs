// ------------------------------------------------------------------------------------------------
// <copyright file="ICommitRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Ports;

/// <summary>The commits of the repositories of the organization of the scope.</summary>
/// <remarks>
///   <para>Every method sees the rows of that organization only, and throws when the scope has not entered one.</para>
///   <para>
///     An instance returned by a method stays attached to the scope: change it, then give it to
///     <see cref="UpdateAsync" />.
///   </para>
/// </remarks>
public interface ICommitRepository
{
  #region Methods

  /// <summary>Finds a commit by its identifier.</summary>
  /// <param name="id">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The commit, or <see langword="null" />.</returns>
  Task<Commit?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Finds a commit of a repository by its SHA.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The commit, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sha" /> is <see langword="null" />.</exception>
  Task<Commit?> FindByShaAsync(long repositoryId, GitSha sha, CancellationToken cancellationToken);

  /// <summary>Lists the commits of the organization that are finalized, were never notified and no longer receive anything.</summary>
  /// <param name="uploadedFrom">The oldest last upload that is looked at, included.</param>
  /// <param name="uploadedBefore">The time before which the last upload must have been received.</param>
  /// <param name="after">
  ///   The last commit of the previous page, or <see langword="null" /> for the first one: the listing goes on after
  ///   it, in the same order, so that a caller that leaves commits out can read past them.
  /// </param>
  /// <param name="limit">The number of commits to list, at most.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The commits, the oldest last upload first, for reading only: complete or in error, of a repository that is
  ///   still tracked, without any upload still received or being processed.
  /// </returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit" /> is zero or negative.</exception>
  Task<IReadOnlyList<Commit>> ListStuckAsync(
    DateTimeOffset uploadedFrom,
    DateTimeOffset uploadedBefore,
    Commit? after,
    int limit,
    CancellationToken cancellationToken);

  /// <summary>Lists one page of the commits of a repository, for reading only.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="branch">The branch the commits were uploaded for, or <see langword="null" /> for every commit.</param>
  /// <param name="page">The number of the page, from 1.</param>
  /// <param name="pageSize">The number of commits of a page.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The page, sorted by completion then identifier, the most recent first; the commits not completed yet come last,
  ///   on both engines.
  /// </returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="page" /> or <paramref name="pageSize" /> is not positive.</exception>
  Task<CommitPage> ListPageAsync(long repositoryId, string? branch, int page, int pageSize, CancellationToken cancellationToken);

  /// <summary>Lists the covered head of the default branch of some repositories, for reading only, in one read.</summary>
  /// <param name="repositoryIds">The identifiers of the repositories.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>One commit per repository whose default branch is covered, with its project totals; in no particular order.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repositoryIds" /> is <see langword="null" />.</exception>
  Task<IReadOnlyList<Commit>> ListCoveredDefaultHeadsAsync(IReadOnlyCollection<long> repositoryIds, CancellationToken cancellationToken);

  /// <summary>Lists the commits of a repository that have one of the given SHAs, for reading only.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="shas">The SHAs to look for.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The commits found, in no particular order; a SHA without a commit gives none.</returns>
  /// <remarks>The instances are not attached to the scope: they show the rows as stored, not a change pending in it.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="shas" /> is <see langword="null" />.</exception>
  Task<IReadOnlyList<Commit>> ListByShasAsync(long repositoryId, IReadOnlyCollection<GitSha> shas, CancellationToken cancellationToken);

  /// <summary>Returns the commit of a SHA, adding it as <see cref="CommitState.Known" /> when it does not exist.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="at">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The commit, whichever concurrent call created it.</returns>
  /// <remarks>
  ///   Inserts first and reads again when the row exists, so that parallel uploads of the same commit all
  ///   succeed. Refused as not found (<c>Repository.NotFound</c>) when the repository is not one of the
  ///   organization of the scope.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="sha" /> is <see langword="null" />.</exception>
  Task<Commit> GetOrCreateAsync(long repositoryId, GitSha sha, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>Saves the changes of a commit returned by this repository in this scope, at once.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the commit is saved.</returns>
  /// <remarks>Refused as a conflict (<c>General.ConcurrentChange</c>) when the row changed or disappeared since it was read.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="commit" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The instance was not read in this scope.</exception>
  Task UpdateAsync(Commit commit, CancellationToken cancellationToken);

  /// <summary>Saves the reception of an upload, set by <see cref="Commit.MarkPending" /> on a commit read in this scope.</summary>
  /// <param name="commit">The commit.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the reception is saved.</returns>
  /// <remarks>
  ///   One atomic statement, with no concurrency check: a finalization saved since the read neither refuses it nor is
  ///   undone by it. A <see cref="CommitState.Known" /> row becomes <see cref="CommitState.Pending" />, the branch and the
  ///   pull request are kept when the row has none, and the dates of the upload are written. Refused as a conflict
  ///   (<c>General.ConcurrentChange</c>) when the row disappeared.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="commit" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The instance was not read in this scope.</exception>
  Task SavePendingAsync(Commit commit, CancellationToken cancellationToken);

  /// <summary>Hands out the next session number of a commit, starting at 1.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The number, which no other call receives.</returns>
  /// <remarks>
  ///   Atomic, and independent of the version of the row: concurrent uploads wait for one another and never
  ///   conflict. Refused as a conflict (<c>General.ConcurrentChange</c>) when the commit is not one of the
  ///   organization of the scope.
  /// </remarks>
  Task<int> NextUploadSequenceAsync(long commitId, CancellationToken cancellationToken);

  #endregion
}
