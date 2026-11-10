// ------------------------------------------------------------------------------------------------
// <copyright file="IComparisonRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Comparisons;

namespace CodeCoverage.Domain.Ports;

/// <summary>Stores the comparison computed for a commit, in the organization of the scope.</summary>
public interface IComparisonRepository
{
  #region Methods

  /// <summary>Finds the comparison stored for a commit.</summary>
  /// <param name="commitId">The identifier of the head commit.</param>
  /// <param name="pullNumber">The number of the pull request, or <see langword="null" /> for the comparison of the push build.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The comparison, or <see langword="null" />.</returns>
  Task<CommitComparison?> FindAsync(long commitId, int? pullNumber, CancellationToken cancellationToken);

  /// <summary>Stores a comparison, in the place of the one already stored for the same commit and pull request.</summary>
  /// <param name="comparison">The comparison.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the comparison is stored.</returns>
  /// <remarks>
  ///   Refused as a conflict (<c>General.ConcurrentChange</c>) when the commit does not exist in the
  ///   organization of the scope. Two saves at the same time leave one row, the one of the last to end.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="comparison" /> is <see langword="null" />.</exception>
  Task SaveAsync(CommitComparison comparison, CancellationToken cancellationToken);

  #endregion
}