// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryFlagRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Notifications;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>RepositoryFlags</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
internal sealed class RepositoryFlagRepository(CodeCoverageDbContext context) : IRepositoryFlagRepository
{
  #region Fields

  private const string RepositoryNotFoundMessage = "The repository does not exist or cannot be read.";

  #endregion

  #region Methods

  /// <summary>Inserts a flag, unless a concurrent save did it first.</summary>
  /// <param name="repositoryId">The identifier of the repository, read in this scope.</param>
  /// <param name="flag">The flag.</param>
  /// <param name="at">The current time, in UTC.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the flag is known.</returns>
  /// <remarks>Nothing is left for a later save of the scope, whether the row was written or refused.</remarks>
  internal async Task InsertAsync(long repositoryId, string flag, DateTimeOffset at, CancellationToken cancellationToken)
  {
    var row = context.RepositoryFlags.Add(new RepositoryFlagRecord { RepositoryId = repositoryId, Flag = flag, CreatedAt = at }).Entity;

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, RepositoryFlagRecordConfiguration.PrimaryKeyName))
    {
      // Another finalization of the repository recorded the flag between the reading and the insertion.
    }
    finally
    {
      context.Entry(row).State = EntityState.Detached;
    }
  }

  #endregion

  #region Implementation of IRepositoryFlagRepository

  /// <inheritdoc />
  public async Task<IReadOnlyList<string>> ListAsync(long repositoryId, CancellationToken cancellationToken)
  {
    var flags = await context.RepositoryFlags
                             .AsNoTracking()
                             .Where(f => f.RepositoryId == repositoryId)
                             .Select(static f => f.Flag)
                             .ToListAsync(cancellationToken)
                             .ConfigureAwait(false);

    // Sorted here: the order of the database follows its collation, which differs between the two engines.
    return [.. flags.Order(StringComparer.Ordinal)];
  }

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository is not one of the organization of the scope.</exception>
  public async Task AddMissingAsync(long repositoryId, IReadOnlyCollection<string> flags, DateTimeOffset at, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(flags);

    if (flags.Count == 0)
    {
      return;
    }

    // The primary key does not hold the organization: a row for the repository of another organization would take its key.
    if (!await context.Repositories.AnyAsync(r => r.Id == repositoryId, cancellationToken).ConfigureAwait(false))
    {
      throw new NotFoundException(RepositoryErrors.NotFound, RepositoryNotFoundMessage);
    }

    var known = (await ListAsync(repositoryId, cancellationToken).ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);
    var utc = at.ToUniversalTime();

    // One by one: a flag refused because a concurrent save recorded it does not take the others with it.
    foreach (var flag in flags.Distinct(StringComparer.Ordinal).Where(flag => !known.Contains(flag)).Order(StringComparer.Ordinal))
    {
      await InsertAsync(repositoryId, flag, utc, cancellationToken).ConfigureAwait(false);
    }
  }

  #endregion
}
