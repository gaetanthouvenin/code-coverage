// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryTreeRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Commits;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>RepositoryTrees</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
internal sealed class RepositoryTreeRepository(CodeCoverageDbContext context) : IRepositoryTreeRepository
{
  #region Fields

  /// <summary>The message of the refusal of an unknown repository; it is sent to the caller.</summary>
  private const string RepositoryNotFoundMessage = "The repository does not exist or cannot be read.";

  #endregion

  #region Implementation of IRepositoryTreeRepository

  /// <inheritdoc />
  public async Task<ForgeTree?> FindAsync(long repositoryId, GitSha sha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sha);

    var stored = await context.RepositoryTrees
                              .AsNoTracking()
                              .SingleOrDefaultAsync(t => t.RepositoryId == repositoryId && t.Sha == sha, cancellationToken)
                              .ConfigureAwait(false);

    return stored is null ? null : new ForgeTree(stored.Sha, RepositoryTreeCodec.Decode(stored.Data), stored.Truncated);
  }

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository is not one of the organization of the scope.</exception>
  /// <remarks>The primary key does not hold the organization, so the repository is read first, under the filter.</remarks>
  public async Task AddAsync(long repositoryId, ForgeTree tree, DateTimeOffset fetchedAt, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(tree);

    var known = await context.Repositories.AnyAsync(r => r.Id == repositoryId, cancellationToken).ConfigureAwait(false);

    if (!known)
    {
      throw new NotFoundException(RepositoryErrors.NotFound, RepositoryNotFoundMessage);
    }

    var row = new RepositoryTreeRecord
    {
      RepositoryId = repositoryId,
      Sha = tree.Sha,
      Data = RepositoryTreeCodec.Encode(tree.Entries),
      Truncated = tree.Truncated,
      FetchedAt = fetchedAt.ToUniversalTime(),
    };

    context.RepositoryTrees.Add(row);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, RepositoryTreeRecordConfiguration.PrimaryKeyName))
    {
      // A tree is immutable: the row a concurrent call added holds the same files.
    }
    finally
    {
      // A cached row is never written again, and its blob has no reason to stay in the context of the scope.
      context.Entry(row).State = EntityState.Detached;
    }
  }

  #endregion
}
