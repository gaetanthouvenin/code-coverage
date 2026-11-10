// ------------------------------------------------------------------------------------------------
// <copyright file="IPostedStatusRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Notifications;

namespace CodeCoverage.Domain.Ports;

/// <summary>Stores the statuses published for the commits, in the organization of the scope.</summary>
public interface IPostedStatusRepository
{
  #region Methods

  /// <summary>Lists the statuses published for a commit.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>One status per context, by context in ordinal order.</returns>
  Task<IReadOnlyList<PostedStatus>> ListByCommitAsync(long commitId, CancellationToken cancellationToken);

  /// <summary>Stores a published status, in the place of the one already stored for the same commit and context.</summary>
  /// <param name="status">The status.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the status is stored.</returns>
  /// <remarks>
  ///   A context is case-sensitive: it can end with a flag. Refused as a conflict
  ///   (<c>General.ConcurrentChange</c>) when the commit does not exist in the organization of the scope.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="status" /> is <see langword="null" />.</exception>
  Task UpsertAsync(PostedStatus status, CancellationToken cancellationToken);

  #endregion
}