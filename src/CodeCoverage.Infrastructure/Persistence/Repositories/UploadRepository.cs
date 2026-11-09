// ------------------------------------------------------------------------------------------------
// <copyright file="UploadRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>Uploads</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>
///   The <c>Find</c> methods track what they return: the shadow column of the organization does not travel with
///   an instance, so a row can only be written by the context that read it (ADR-013).
/// </remarks>
internal sealed class UploadRepository(CodeCoverageDbContext context) : IUploadRepository
{
  #region Fields

  /// <summary>The message of the refusal of an upload whose key is already used; it is sent to the caller.</summary>
  private const string DuplicateMessage = "The upload was received in the meantime; reload it and try again.";

  /// <summary>The message of the refusal of a commit that cannot be read; it is sent to the caller.</summary>
  private const string CommitGoneMessage = "The commit was changed or removed in the meantime; reload it and try again.";

  #endregion

  #region Implementation of IUploadRepository

  /// <inheritdoc />
  public async Task<Upload?> FindByPublicIdAsync(Guid publicId, CancellationToken cancellationToken) =>
    await context.Uploads
                 .SingleOrDefaultAsync(u => u.PublicId == publicId, cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<Upload?> FindByIdempotencyKeyAsync(long commitId, string idempotencyKey, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(idempotencyKey);

    return await context.Uploads
                        .SingleOrDefaultAsync(u => u.CommitId == commitId && u.IdempotencyKey == idempotencyKey, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<Upload>> ListByCommitAsync(long commitId, CancellationToken cancellationToken) =>
    await context.Uploads
                 .AsNoTracking()
                 .Where(u => u.CommitId == commitId)
                 .OrderBy(static u => u.Sequence)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<bool> TryStartProcessingAsync(Guid publicId, string worker, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(worker);

    var updated = await context.Uploads
                               .Where(u => u.PublicId == publicId && (u.State == UploadState.Received || u.State == UploadState.Processing))
                               .ExecuteUpdateAsync(
                                 setters => setters.SetProperty(static u => u.State, UploadState.Processing).SetProperty(static u => u.LockedBy, worker),
                                 cancellationToken
                               )
                               .ConfigureAwait(false);

    return updated > 0;
  }

  /// <inheritdoc />
  /// <exception cref="ConflictException">The commit cannot be read, or the idempotency key is already used on it.</exception>
  /// <remarks>
  ///   The unique index does not hold the organization, so a row written for the commit of another organization
  ///   would take its idempotency key there: the commit is read first, under the filter.
  /// </remarks>
  public async Task AddAsync(Upload upload, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(upload);

    var commitId = upload.CommitId;
    var known = await context.Commits.AnyAsync(c => c.Id == commitId, cancellationToken).ConfigureAwait(false);

    if (!known)
    {
      throw new ConflictException(GeneralErrors.ConcurrentChange, CommitGoneMessage);
    }

    context.Uploads.Add(upload);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, UploadConfiguration.IdempotencyIndexName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(upload).State = EntityState.Detached;

      throw new ConflictException(GeneralErrors.ConcurrentChange, DuplicateMessage, exception);
    }
  }

  #endregion
}
