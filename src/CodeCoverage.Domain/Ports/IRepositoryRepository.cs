// ------------------------------------------------------------------------------------------------
// <copyright file="IRepositoryRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Ports;

/// <summary>The repositories tracked by the organization of the scope.</summary>
/// <remarks>
///   <para>Every method sees the rows of that organization only, and throws when the scope has not entered one.</para>
///   <para>
///     An instance returned by a <c>Find</c> method stays attached to the scope: change it, then give it to
///     <see cref="UpdateAsync" />. An instance from <see cref="ListAsync" /> is for reading only.
///   </para>
/// </remarks>
public interface IRepositoryRepository
{
  #region Methods

  /// <summary>Finds a repository by its identifier.</summary>
  /// <param name="id">The identifier of the repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repository, or <see langword="null" />.</returns>
  Task<TrackedRepository?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Finds a repository by its namespace and its name, whatever their case.</summary>
  /// <param name="slug">The name of the repository; its owner is not compared.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repository, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="slug" /> is <see langword="null" />.</exception>
  Task<TrackedRepository?> FindBySlugAsync(RepositorySlug slug, CancellationToken cancellationToken);

  /// <summary>Finds a repository by the identifier its forge gives it, whatever its case.</summary>
  /// <param name="externalId">The identifier given by the forge.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repository, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="externalId" /> is <see langword="null" />.</exception>
  Task<TrackedRepository?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken);

  /// <summary>Lists the tracked repositories, for reading only.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repositories, by namespace and name.</returns>
  Task<IReadOnlyList<TrackedRepository>> ListAsync(CancellationToken cancellationToken);

  /// <summary>Adds a repository, saved at once.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the repository is saved.</returns>
  /// <remarks>
  ///   Refused as a conflict (<c>General.ConcurrentChange</c>) when a concurrent save added the same name or
  ///   external identifier: reading again finds its row.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
  Task AddAsync(TrackedRepository repository, CancellationToken cancellationToken);

  /// <summary>Saves the changes of a repository returned by a <c>Find</c> method of this scope, at once.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the repository is saved.</returns>
  /// <remarks>Refused as a conflict (<c>General.ConcurrentChange</c>) when the row changed or disappeared since it was read.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The instance was not read in this scope.</exception>
  Task UpdateAsync(TrackedRepository repository, CancellationToken cancellationToken);

  /// <summary>Rewrites the owner of the next repositories of the organization, in the order of their identifiers.</summary>
  /// <param name="owner">The login of the account that owns them.</param>
  /// <param name="afterId">The identifier the batch starts after; 0 for the first batch.</param>
  /// <param name="batchSize">How many repositories one call rewrites at most.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The identifier of the last repository of the batch, or <see langword="null" /> when none is left.</returns>
  /// <remarks>
  ///   The owner is a copy for display: the rows are written at once, inactive ones included, without their
  ///   <c>Version</c> nor their date of update, so no save in progress is refused because of it. Running it again
  ///   changes nothing more.
  /// </remarks>
  /// <exception cref="ArgumentException"><paramref name="owner" /> is not a well-formed login.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="afterId" /> is negative, or <paramref name="batchSize" /> is zero or negative.</exception>
  Task<long?> RewriteOwnerAsync(string owner, long afterId, int batchSize, CancellationToken cancellationToken);

  /// <summary>Saves a repository, new or returned by a <c>Find</c> method of this scope, and queues a job in the same transaction.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="job">The job that the change calls for.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the repository is saved and the job queued.</returns>
  /// <remarks>
  ///   Refused as a conflict (<c>General.ConcurrentChange</c>) when a concurrent save added the same repository
  ///   or changed its row: nothing is saved and nothing is queued.
  /// </remarks>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  Task SaveWithJobAsync(TrackedRepository repository, JobRequest job, CancellationToken cancellationToken);

  /// <summary>
  ///   Saves a repository that left the organization and, in the same transaction, revokes its upload tokens
  ///   and advises the rotation of the tokens of the organization.
  /// </summary>
  /// <param name="repository">The repository, returned by a <c>Find</c> method of this scope.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>How many tokens were revoked and how many were marked.</returns>
  /// <remarks>Refused as a conflict (<c>General.ConcurrentChange</c>) when the row changed or disappeared since it was read.</remarks>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The instance was not read in this scope.</exception>
  Task<RepositoryDepartureTokens> SaveDepartedAsync(TrackedRepository repository, DateTimeOffset at, CancellationToken cancellationToken);

  #endregion
}
