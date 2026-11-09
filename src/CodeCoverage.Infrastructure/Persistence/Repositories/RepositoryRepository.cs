// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>Repositories</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>
///   The <c>Find</c> methods track what they return: the shadow column of the organization does not travel with
///   an instance, so a row can only be written by the context that read it (ADR-013), and the original
///   <c>Version</c> it holds is what detects a concurrent write.
/// </remarks>
internal sealed class RepositoryRepository(CodeCoverageDbContext context) : IRepositoryRepository
{
  #region Fields

  /// <summary>The message of the refusal of a repository added by another save meanwhile; it is sent to the caller.</summary>
  private const string DuplicateMessage = "The repository was added in the meantime; reload it and try again.";

  /// <summary>The message of the refusal of an instance that this context did not read.</summary>
  private const string NotReadMessage = "A repository must be read in this scope before it can be updated.";

  #endregion

  #region Implementation of IRepositoryRepository

  /// <inheritdoc />
  public async Task<TrackedRepository?> FindAsync(long id, CancellationToken cancellationToken) =>
    await context.Repositories
                 .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<TrackedRepository?> FindBySlugAsync(RepositorySlug slug, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(slug);

    var normalizedNamespace = slug.NormalizedNamespace;
    var normalizedName = slug.NormalizedName;

    return await context.Repositories
                        .SingleOrDefaultAsync(
                          r => r.NormalizedNamespace == normalizedNamespace && r.NormalizedName == normalizedName,
                          cancellationToken
                        )
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<TrackedRepository?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(externalId);

    var normalizedId = TrackedRepository.NormalizeExternalId(externalId);

    return await context.Repositories
                        .SingleOrDefaultAsync(r => r.ExternalId == normalizedId, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<TrackedRepository>> ListAsync(CancellationToken cancellationToken) =>
    await context.Repositories
                 .AsNoTracking()
                 .OrderBy(static r => r.NormalizedNamespace)
                 .ThenBy(static r => r.NormalizedName)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  /// <exception cref="ConflictException">A concurrent save added the same name or external identifier.</exception>
  public async Task AddAsync(TrackedRepository repository, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    context.Repositories.Add(repository);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (IsDuplicate(exception))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(repository).State = EntityState.Detached;

      throw new ConflictException(GeneralErrors.ConcurrentChange, DuplicateMessage, exception);
    }
  }

  /// <inheritdoc />
  public async Task UpdateAsync(TrackedRepository repository, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);

    InvalidOperationException.ThrowIf(context.Entry(repository).State is EntityState.Detached, NotReadMessage);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion

  #region Methods

  /// <summary>Tells whether a save was refused by a unique index of the table.</summary>
  /// <param name="exception">The failure of the save.</param>
  /// <returns><see langword="true" /> when another row holds the same name or external identifier.</returns>
  private static bool IsDuplicate(DbUpdateException exception) =>
    UniqueIndexViolation.Is(exception, TrackedRepositoryConfiguration.NameIndexName)
    || UniqueIndexViolation.Is(exception, TrackedRepositoryConfiguration.ExternalIdIndexName);

  #endregion
}
