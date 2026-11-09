// ------------------------------------------------------------------------------------------------
// <copyright file="GitCommitRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>GitCommits</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
internal sealed class GitCommitRepository(CodeCoverageDbContext context) : IGitCommitRepository
{
  #region Fields

  /// <summary>The message of the refusal of an unknown repository; it is sent to the caller.</summary>
  private const string RepositoryNotFoundMessage = "The repository does not exist or cannot be read.";

  #endregion

  #region Implementation of IGitCommitRepository

  /// <inheritdoc />
  public async Task<GitCommit?> FindAsync(long repositoryId, GitSha sha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sha);

    return await context.GitCommits
                        .AsNoTracking()
                        .SingleOrDefaultAsync(g => g.RepositoryId == repositoryId && g.Sha == sha, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository is not one of the organization of the scope.</exception>
  /// <remarks>
  ///   Each row is saved alone: in a batch, the one row a concurrent call added would refuse all the others.
  ///   The primary key does not hold the organization, so the repository is read first, under the filter.
  /// </remarks>
  public async Task AddMissingAsync(
    long repositoryId,
    IReadOnlyCollection<ForgeCommitInfo> commits,
    DateTimeOffset fetchedAt,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(commits);

    var known = await context.Repositories.AnyAsync(r => r.Id == repositoryId, cancellationToken).ConfigureAwait(false);

    if (!known)
    {
      throw new NotFoundException(RepositoryErrors.NotFound, RepositoryNotFoundMessage);
    }

    var shas = commits.Select(static c => c.Sha).Distinct().ToList();

    var cached = await context.GitCommits
                              .AsNoTracking()
                              .Where(g => g.RepositoryId == repositoryId && shas.Contains(g.Sha))
                              .Select(static g => g.Sha)
                              .ToListAsync(cancellationToken)
                              .ConfigureAwait(false);

    foreach (var commit in commits.DistinctBy(static c => c.Sha).ExceptBy(cached, static c => c.Sha))
    {
      await AddUnlessPresentAsync(GitCommit.Create(repositoryId, commit, fetchedAt), cancellationToken).ConfigureAwait(false);
    }
  }

  #endregion

  #region Methods

  /// <summary>Saves a row, leaving the one a concurrent call added meanwhile as it is.</summary>
  /// <param name="row">The row.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the row is present.</returns>
  private async Task AddUnlessPresentAsync(GitCommit row, CancellationToken cancellationToken)
  {
    context.GitCommits.Add(row);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, GitCommitConfiguration.PrimaryKeyName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(row).State = EntityState.Detached;
    }
  }

  #endregion
}
