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
