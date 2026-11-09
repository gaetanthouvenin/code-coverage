// ------------------------------------------------------------------------------------------------
// <copyright file="UserRepositoryAccessRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>UserRepositoryAccess</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
internal sealed class UserRepositoryAccessRepository(CodeCoverageDbContext context) : IUserRepositoryAccessRepository
{
  #region Fields

  /// <summary>The message of the refusal of a row that can no longer be read; it is sent to the caller.</summary>
  private const string AccessGoneMessage = "The access was changed or removed in the meantime; reload it and try again.";

  #endregion

  #region Methods

  /// <summary>Reads an access, tracked: a row can only be written by the context that read it (ADR-013).</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="normalizedExternalId">The identifier of the repository, in lower case.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The access, or <see langword="null" />.</returns>
  private async Task<RepositoryAccess?> FindTrackedAsync(long userId, string normalizedExternalId, CancellationToken cancellationToken) =>
    await context.UserRepositoryAccess
                 .SingleOrDefaultAsync(a => a.UserId == userId && a.RepositoryExternalId == normalizedExternalId, cancellationToken)
                 .ConfigureAwait(false);

  /// <summary>Applies what the forge reports to a row read in this scope, and saves it.</summary>
  /// <param name="access">The access.</param>
  /// <param name="permission">The access level.</param>
  /// <param name="at">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The access.</returns>
  private async Task<RepositoryAccess> RefreshAsync(RepositoryAccess access, RepositoryPermission permission, DateTimeOffset at, CancellationToken cancellationToken)
  {
    access.Refresh(permission, at);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    return access;
  }

  #endregion

  #region Implementation of IUserRepositoryAccessRepository

  /// <inheritdoc />
  public async Task<RepositoryAccess?> FindAsync(long userId, string repositoryExternalId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repositoryExternalId);

    var normalized = TrackedRepository.NormalizeExternalId(repositoryExternalId);

    return await context.UserRepositoryAccess
                        .AsNoTracking()
                        .SingleOrDefaultAsync(a => a.UserId == userId && a.RepositoryExternalId == normalized, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<RepositoryAccess>> ListOfUserAsync(long userId, CancellationToken cancellationToken) =>
    await context.UserRepositoryAccess
                 .AsNoTracking()
                 .Where(a => a.UserId == userId)
                 .OrderBy(static a => a.RepositoryExternalId)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  /// <exception cref="ConflictException">The row that refused the insertion can no longer be read, or changed in the meantime.</exception>
  /// <remarks>A concurrent call that inserted the row first makes the insertion fail on the index: the row is then read and updated.</remarks>
  public async Task<RepositoryAccess> UpsertAsync(long userId, string repositoryExternalId, RepositoryPermission permission, DateTimeOffset at, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repositoryExternalId);

    var added = RepositoryAccess.Create(userId, repositoryExternalId, permission, at);
    var normalized = added.RepositoryExternalId;
    var existing = await FindTrackedAsync(userId, normalized, cancellationToken).ConfigureAwait(false);

    if (existing is not null)
    {
      return await RefreshAsync(existing, permission, at, cancellationToken).ConfigureAwait(false);
    }

    context.UserRepositoryAccess.Add(added);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

      return added;
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, RepositoryAccessConfiguration.AccessIndexName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(added).State = EntityState.Detached;

      var winner = await FindTrackedAsync(userId, normalized, cancellationToken).ConfigureAwait(false)
                   ?? throw new ConflictException(GeneralErrors.ConcurrentChange, AccessGoneMessage, exception);

      return await RefreshAsync(winner, permission, at, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <inheritdoc />
  public async Task RemoveAsync(long userId, string repositoryExternalId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repositoryExternalId);

    var normalized = TrackedRepository.NormalizeExternalId(repositoryExternalId);

    await context.UserRepositoryAccess
                 .Where(a => a.UserId == userId && a.RepositoryExternalId == normalized)
                 .ExecuteDeleteAsync(cancellationToken)
                 .ConfigureAwait(false);
  }

  #endregion
}
