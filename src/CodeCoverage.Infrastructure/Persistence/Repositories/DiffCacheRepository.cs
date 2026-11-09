// ------------------------------------------------------------------------------------------------
// <copyright file="DiffCacheRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Commits;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>DiffCache</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
internal sealed class DiffCacheRepository(CodeCoverageDbContext context) : IDiffCacheRepository
{
  #region Fields

  /// <summary>The message of the refusal of an unknown repository; it is sent to the caller.</summary>
  private const string RepositoryNotFoundMessage = "The repository does not exist or cannot be read.";

  #endregion

  #region Implementation of IDiffCacheRepository

  /// <inheritdoc />
  public async Task<DiffSet?> FindAsync(long repositoryId, GitSha baseSha, GitSha headSha, DiffSource source, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(baseSha);
    ArgumentNullException.ThrowIfNull(headSha);

    var stored = await context.DiffCache
                              .AsNoTracking()
                              .SingleOrDefaultAsync(
                                d => d.RepositoryId == repositoryId && d.BaseSha == baseSha && d.HeadSha == headSha && d.Kind == source,
                                cancellationToken
                              )
                              .ConfigureAwait(false);

    return stored is null ? null : new DiffSet(DiffCacheCodec.Decode(stored.Data), stored.Incomplete, stored.Kind);
  }

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository is not one of the organization of the scope.</exception>
  /// <remarks>The primary key does not hold the organization, so the repository is read first, under the filter.</remarks>
  public async Task AddAsync(long repositoryId, GitSha baseSha, GitSha headSha, DiffSet diff, DateTimeOffset fetchedAt, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(baseSha);
    ArgumentNullException.ThrowIfNull(headSha);
    ArgumentNullException.ThrowIfNull(diff);

    var known = await context.Repositories.AnyAsync(r => r.Id == repositoryId, cancellationToken).ConfigureAwait(false);

    if (!known)
    {
      throw new NotFoundException(RepositoryErrors.NotFound, RepositoryNotFoundMessage);
    }

    var row = new DiffCacheRecord
    {
      RepositoryId = repositoryId,
      BaseSha = baseSha,
      HeadSha = headSha,
      Kind = diff.Source,
      Data = DiffCacheCodec.Encode(diff.Files),
      Incomplete = diff.Truncated,
      FetchedAt = fetchedAt.ToUniversalTime(),
    };

    context.DiffCache.Add(row);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, DiffCacheRecordConfiguration.PrimaryKeyName))
    {
      // A diff is immutable: the row a concurrent call added holds the same files.
    }
    finally
    {
      // A cached row is never written again, and its blob has no reason to stay in the context of the scope.
      context.Entry(row).State = EntityState.Detached;
    }
  }

  #endregion
}
