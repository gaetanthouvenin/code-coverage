// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>Repositories</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="jobQueue">The queue that takes a job in the transaction of the change.</param>
/// <param name="wakeUpSignal">The signal that wakes the worker of this process once a job is committed.</param>
/// <remarks>
///   The <c>Find</c> methods track what they return: the shadow column of the organization does not travel with
///   an instance, so a row can only be written by the context that read it (ADR-013), and the original
///   <c>Version</c> it holds is what detects a concurrent write.
/// </remarks>
internal sealed class RepositoryRepository(CodeCoverageDbContext context, SqlJobQueue jobQueue, JobWakeUpSignal wakeUpSignal) : IRepositoryRepository
{
  #region Fields

  /// <summary>The message of the refusal of a repository added by another save meanwhile; it is sent to the caller.</summary>
  private const string DuplicateMessage = "The repository was added in the meantime; reload it and try again.";

  /// <summary>The message of the refusal of an instance that this context did not read.</summary>
  private const string NameTakenMessage = "Another repository of the organization holds this name; reload and try again.";

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
  public async Task<long?> RewriteOwnerAsync(string owner, long afterId, int batchSize, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIf(!Organization.IsWellFormedLogin(owner), $"The login '{owner}' is not well formed.", nameof(owner));
    ArgumentOutOfRangeException.ThrowIfNegative(afterId);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

    var ids = await context.Repositories
                           .Where(r => r.Id > afterId)
                           .OrderBy(static r => r.Id)
                           .Select(static r => r.Id)
                           .Take(batchSize)
                           .ToListAsync(cancellationToken)
                           .ConfigureAwait(false);

    if (ids.Count == 0)
    {
      return null;
    }

    var last = ids[^1];

    // Every row of the batch is written, whatever it holds: comparing the stored owner with the new one would
    // skip a rename that only changes the case, on a database whose collation ignores it.
    await context.Repositories
                 .Where(r => r.Id > afterId && r.Id <= last)
                 .ExecuteUpdateAsync(setters => setters.SetProperty(static r => r.Owner, owner), cancellationToken)
                 .ConfigureAwait(false);

    return last;
  }

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

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (IsDuplicate(exception))
    {
      Revert(repository);

      throw new ConflictException(GeneralErrors.ConcurrentChange, NameTakenMessage, exception);
    }
  }

  /// <inheritdoc />
  public async Task SaveWithJobAsync(TrackedRepository repository, JobRequest job, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    ArgumentNullException.ThrowIfNull(job);

    var added = context.Entry(repository).State is EntityState.Detached;
    long jobId;

    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      if (added)
      {
        context.Repositories.Add(repository);
      }

      try
      {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        jobId = await jobQueue.EnqueueInTransactionAsync(context, job, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }
      catch (DbUpdateException exception) when (added && IsDuplicate(exception))
      {
        // Nothing is left for a later save of the scope, which would fail the same way.
        context.Entry(repository).State = EntityState.Detached;

        throw new ConflictException(GeneralErrors.ConcurrentChange, DuplicateMessage, exception);
      }
      catch (DbUpdateException exception) when (IsDuplicate(exception))
      {
        Revert(repository);

        throw new ConflictException(GeneralErrors.ConcurrentChange, NameTakenMessage, exception);
      }
      catch when (added)
      {
        context.Entry(repository).State = EntityState.Detached;

        throw;
      }
    }

    // Committed: a worker of this process may claim the job at once instead of at its next poll.
    wakeUpSignal.Notify(jobId);
  }

  /// <inheritdoc />
  public async Task<RepositoryDepartureTokens> SaveDepartedAsync(TrackedRepository repository, DateTimeOffset at, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repository);
    InvalidOperationException.ThrowIf(context.Entry(repository).State is EntityState.Detached, NotReadMessage);

    var repositoryId = repository.Id;
    var utc = at.ToUniversalTime();
    var closing = context.Entry(repository).State is EntityState.Modified;

    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

      var revoked = await context.UploadTokens
                                 .Where(t => t.Scope == UploadTokenScope.Repository && t.RepositoryId == repositoryId && t.RevokedAt == null)
                                 .ExecuteUpdateAsync(setters => setters.SetProperty(static t => t.RevokedAt, utc), cancellationToken)
                                 .ConfigureAwait(false);

      // A token of the organization may sit in the secrets of the repository that left: revoking it would
      // break the other repositories, so it is only marked (ADR-013).
      // Only when this save closes something: a departure saved again must not mark the tokens the
      // organization created since, to follow the advice.
      var advised = closing || revoked > 0
                      ? await context.UploadTokens
                                     .Where(static t => t.Scope == UploadTokenScope.Organization && t.RevokedAt == null)
                                     .ExecuteUpdateAsync(setters => setters.SetProperty(static t => t.RotationAdvisedAt, utc), cancellationToken)
                                     .ConfigureAwait(false)
                      : 0;

      await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

      return new RepositoryDepartureTokens(revoked, advised);
    }
  }

  #endregion

  #region Methods

  /// <summary>Puts a tracked repository back to the values of its row, after a save that the database refused.</summary>
  /// <param name="repository">The repository.</param>
  /// <remarks>Nothing is left for a later save of the scope, which would fail the same way.</remarks>
  private void Revert(TrackedRepository repository)
  {
    var entry = context.Entry(repository);

    entry.CurrentValues.SetValues(entry.OriginalValues);
    entry.State = EntityState.Unchanged;
  }

  /// <summary>Tells whether a save was refused by a unique index of the table.</summary>
  /// <param name="exception">The failure of the save.</param>
  /// <returns><see langword="true" /> when another row holds the same name or external identifier.</returns>
  private static bool IsDuplicate(DbUpdateException exception) =>
    UniqueIndexViolation.Is(exception, TrackedRepositoryConfiguration.NameIndexName)
    || UniqueIndexViolation.Is(exception, TrackedRepositoryConfiguration.ExternalIdIndexName);

  #endregion
}
