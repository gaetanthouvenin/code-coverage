// ------------------------------------------------------------------------------------------------
// <copyright file="IUploadRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Ports;

/// <summary>The uploads of the commits of the organization of the scope.</summary>
/// <remarks>Every method sees the rows of that organization only, and throws when the scope has not entered one.</remarks>
public interface IUploadRepository
{
  #region Methods

  /// <summary>Finds an upload by its public identifier.</summary>
  /// <param name="publicId">The public identifier.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The upload, or <see langword="null" />.</returns>
  Task<Upload?> FindByPublicIdAsync(Guid publicId, CancellationToken cancellationToken);

  /// <summary>Finds the upload that holds an idempotency key on a commit.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="idempotencyKey">The key.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The upload, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="idempotencyKey" /> is <see langword="null" />.</exception>
  Task<Upload?> FindByIdempotencyKeyAsync(long commitId, string idempotencyKey, CancellationToken cancellationToken);

  /// <summary>Lists the uploads of a commit, for reading only.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The uploads, by session number.</returns>
  Task<IReadOnlyList<Upload>> ListByCommitAsync(long commitId, CancellationToken cancellationToken);

  /// <summary>Takes an upload to process it, unless its processing has ended.</summary>
  /// <param name="publicId">The public identifier of the upload.</param>
  /// <param name="worker">The worker that takes it.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns><see langword="true" /> when it was taken, a retry of its job included; otherwise <see langword="false" />.</returns>
  Task<bool> TryStartProcessingAsync(Guid publicId, string worker, CancellationToken cancellationToken);

  /// <summary>Moves an upload to the commit of another SHA of its repository, added when it does not exist, saved at once.</summary>
  /// <param name="upload">The upload, read in this scope.</param>
  /// <param name="targetSha">The SHA of the commit that receives it.</param>
  /// <param name="at">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The commit that received it.</returns>
  /// <remarks>
  ///   The other changes of the upload are saved with the move, or before it. Refused as a conflict
  ///   (<c>Upload.Duplicate</c>) when the commit that receives it already holds the same idempotency key: a replay of
  ///   an upload already moved, for good. The upload then keeps its commit and its number.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="upload" /> or <paramref name="targetSha" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The upload was not read in this scope.</exception>
  Task<Commit> MoveToCommitAsync(Upload upload, GitSha targetSha, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>Adds an upload, saved at once.</summary>
  /// <param name="upload">The upload.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the upload is saved.</returns>
  /// <remarks>
  ///   Refused as a conflict (<c>General.ConcurrentChange</c>) when the commit is not one of the organization
  ///   of the scope, or when its idempotency key is already used on the commit: the upload is a replay, and
  ///   <see cref="FindByIdempotencyKeyAsync" /> finds the original.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="upload" /> is <see langword="null" />.</exception>
  Task AddAsync(Upload upload, CancellationToken cancellationToken);

  #endregion
}
