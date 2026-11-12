// ------------------------------------------------------------------------------------------------
// <copyright file="IRepositoryFlagRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Ports;

/// <summary>Stores the flags a repository received coverage for, in the organization of the scope.</summary>
/// <remarks>
///   The flags known to a repository are the ones that get a flag status (ARCHITECTURE §7.7), as the
///   <c>repository.flags</c> of Codecov: a flag that was uploaded once keeps its status on the commits that
///   do not carry it, and a flag that is only declared in the configuration has none.
/// </remarks>
public interface IRepositoryFlagRepository
{
  #region Methods

  /// <summary>Lists the flags known to a repository.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The flags, case-sensitive, in ordinal order.</returns>
  Task<IReadOnlyList<string>> ListAsync(long repositoryId, CancellationToken cancellationToken);

  /// <summary>Lists the flags known to some repositories, in one read.</summary>
  /// <param name="repositoryIds">The identifiers of the repositories.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The flags of each repository that has one, case-sensitive, in ordinal order.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repositoryIds" /> is <see langword="null" />.</exception>
  Task<IReadOnlyDictionary<long, IReadOnlyList<string>>> ListByRepositoriesAsync(IReadOnlyCollection<long> repositoryIds, CancellationToken cancellationToken);

  /// <summary>Records the flags of a repository that are not known yet.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="flags">The flags its sessions carry, case-sensitive; the ones already known are left as they are.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when every flag is known.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="flags" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   Two finalizations of the same repository can record the same flag at once: the one that comes second
  ///   finds it known, and neither fails.
  /// </remarks>
  Task AddMissingAsync(long repositoryId, IReadOnlyCollection<string> flags, DateTimeOffset at, CancellationToken cancellationToken);

  #endregion
}
