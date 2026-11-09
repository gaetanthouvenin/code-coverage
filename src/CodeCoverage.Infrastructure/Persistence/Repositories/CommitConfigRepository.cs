// ------------------------------------------------------------------------------------------------
// <copyright file="CommitConfigRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>CommitConfigs</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
internal sealed class CommitConfigRepository(CodeCoverageDbContext context) : ICommitConfigRepository
{
  #region Fields

  /// <summary>The message of the refusal of an unknown repository; it is sent to the caller.</summary>
  private const string RepositoryNotFoundMessage = "The repository does not exist or cannot be read.";

  #endregion

  #region Implementation of ICommitConfigRepository

  /// <inheritdoc />
  public async Task<CommitConfig?> FindAsync(long repositoryId, GitSha sha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sha);

    return await context.CommitConfigs
                        .AsNoTracking()
                        .SingleOrDefaultAsync(c => c.RepositoryId == repositoryId && c.Sha == sha, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository is not one of the organization of the scope.</exception>
  /// <remarks>The primary key does not hold the organization, so the repository is read first, under the filter.</remarks>
  public async Task AddAsync(CommitConfig config, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(config);

    var repositoryId = config.RepositoryId;
    var known = await context.Repositories.AnyAsync(r => r.Id == repositoryId, cancellationToken).ConfigureAwait(false);

    if (!known)
    {
      throw new NotFoundException(RepositoryErrors.NotFound, RepositoryNotFoundMessage);
    }

    context.CommitConfigs.Add(config);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, CommitConfigConfiguration.PrimaryKeyName))
    {
      // What a commit holds does not change: the row a concurrent call added says the same.
    }
    finally
    {
      // A cached row is never written again.
      context.Entry(config).State = EntityState.Detached;
    }
  }

  #endregion
}
