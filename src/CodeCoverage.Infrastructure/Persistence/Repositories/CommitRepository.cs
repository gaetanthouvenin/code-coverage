// ------------------------------------------------------------------------------------------------
// <copyright file="CommitRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>Commits</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>
///   The <c>Find</c> methods and <see cref="GetOrCreateAsync" /> track what they return: the shadow column of the
///   organization does not travel with an instance, so a row can only be written by the context that read it
///   (ADR-013), and the original <c>Version</c> it holds is what detects a concurrent write.
/// </remarks>
internal sealed class CommitRepository(CodeCoverageDbContext context) : ICommitRepository
{
  #region Fields

  /// <summary>The message of the refusal of a commit that this context did not read.</summary>
  private const string NotReadMessage = "A commit must be read in this scope before it can be updated.";

  /// <summary>The message of the refusal of an unknown repository; it is sent to the caller.</summary>
  private const string RepositoryNotFoundMessage = "The repository does not exist or cannot be read.";

  /// <summary>The message of the refusal of a commit that cannot be read; it is sent to the caller.</summary>
  private const string CommitGoneMessage = "The commit was changed or removed in the meantime; reload it and try again.";

  #endregion

  #region Implementation of ICommitRepository

  /// <inheritdoc />
  public async Task<Commit?> FindAsync(long id, CancellationToken cancellationToken) =>
    await context.Commits
                 .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<Commit?> FindByShaAsync(long repositoryId, GitSha sha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sha);

    return await context.Commits
                        .SingleOrDefaultAsync(c => c.RepositoryId == repositoryId && c.Sha == sha, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<Commit>> ListStuckAsync(
    DateTimeOffset uploadedFrom,
    DateTimeOffset uploadedBefore,
    Commit? after,
    int limit,
    CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

    // The times are those of the clock of the application, as everywhere they are written: never the one of the server.
    var from = uploadedFrom.ToUniversalTime();
    var before = uploadedBefore.ToUniversalTime();

    // The order of the listing, (LastUploadAt, Id), goes on after the last commit of the previous page.
    var afterAt = after?.LastUploadAt?.ToUniversalTime();
    var afterId = after?.Id ?? 0;

    return await context.Commits
                        .AsNoTracking()
                        .Where(c => afterAt == null || c.LastUploadAt > afterAt || (c.LastUploadAt == afterAt && c.Id > afterId))
                        .Where(c => c.NotifiedAt == null
                                    && c.LastUploadAt != null
                                    && c.LastUploadAt >= from
                                    && c.LastUploadAt < before
                                    && (c.State == CommitState.Complete || c.State == CommitState.Error)
                                    && context.Repositories.Any(r => r.Id == c.RepositoryId && r.IsActive && r.DisabledAt == null)
                                    && !context.Uploads.Any(u => u.CommitId == c.Id && (u.State == UploadState.Received || u.State == UploadState.Processing)))
                        .OrderBy(static c => c.LastUploadAt)
                        .ThenBy(static c => c.Id)
                        .Take(limit)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<Commit>> ListCoveredDefaultHeadsAsync(IReadOnlyCollection<long> repositoryIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(repositoryIds);

    if (repositoryIds.Count == 0)
    {
      return [];
    }

    var wanted = repositoryIds.Distinct().ToList();

    return await (from repository in context.Repositories.AsNoTracking()
                  where wanted.Contains(repository.Id)
                  join branch in context.Branches.AsNoTracking()
                    on new { RepositoryId = repository.Id, Name = repository.DefaultBranch } equals new { branch.RepositoryId, branch.Name }
                  join commit in context.Commits.AsNoTracking() on branch.CoveredCommitId equals commit.Id
                  select commit).ToListAsync(cancellationToken)
                                .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<CommitPage> ListPageAsync(long repositoryId, string? branch, int page, int pageSize, CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

    var commits = context.Commits.AsNoTracking().Where(c => c.RepositoryId == repositoryId);

    if (branch is not null)
    {
      commits = commits.Where(c => c.Branch == branch);
    }

    var total = await commits.CountAsync(cancellationToken).ConfigureAwait(false);

    // PostgreSQL sorts the nulls first in a descending order and SQL Server last: the order says it.
    var items = await commits.OrderBy(static c => c.CompletedAt == null ? 1 : 0)
                             .ThenByDescending(static c => c.CompletedAt)
                             .ThenByDescending(static c => c.Id)
                             .Skip((page - 1) * pageSize)
                             .Take(pageSize)
                             .ToListAsync(cancellationToken)
                             .ConfigureAwait(false);

    return new CommitPage(items, page, pageSize, total);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<Commit>> ListByShasAsync(long repositoryId, IReadOnlyCollection<GitSha> shas, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(shas);

    if (shas.Count == 0)
    {
      return [];
    }

    return await context.Commits
                        .AsNoTracking()
                        .Where(c => c.RepositoryId == repositoryId && shas.Contains(c.Sha))
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository is not one of the organization of the scope.</exception>
  /// <exception cref="ConflictException">The row that refused the insertion can no longer be read.</exception>
  /// <remarks>
  ///   The insertion comes first so that every loser of a race takes the same path, on both engines: the
  ///   database makes it wait for the winner, then refuses it, and the row is committed when it is read again.
  ///   The unique index does not hold the organization, so the repository is read first, under the filter.
  /// </remarks>
  public async Task<Commit> GetOrCreateAsync(
    long repositoryId,
    GitSha sha,
    DateTimeOffset at,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sha);

    var known = await context.Repositories.AnyAsync(r => r.Id == repositoryId, cancellationToken).ConfigureAwait(false);

    if (!known)
    {
      throw new NotFoundException(RepositoryErrors.NotFound, RepositoryNotFoundMessage);
    }

    var commit = Commit.Create(repositoryId, sha, at);
    context.Commits.Add(commit);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

      return commit;
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, CommitConfiguration.ShaIndexName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(commit).State = EntityState.Detached;

      return await FindByShaAsync(repositoryId, sha, cancellationToken).ConfigureAwait(false)
             ?? throw new ConflictException(GeneralErrors.ConcurrentChange, CommitGoneMessage, exception);
    }
  }

  /// <inheritdoc />
  public async Task UpdateAsync(Commit commit, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(commit);

    InvalidOperationException.ThrowIf(context.Entry(commit).State is EntityState.Detached, NotReadMessage);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="ConflictException">The row disappeared.</exception>
  public async Task SavePendingAsync(Commit commit, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(commit);

    var entry = context.Entry(commit);

    InvalidOperationException.ThrowIf(entry.State is EntityState.Detached, NotReadMessage);

    var commitId = commit.Id;
    var branch = commit.Branch;
    var pullNumber = commit.PullNumber;
    var lastUploadAt = commit.LastUploadAt;
    var updatedAt = commit.UpdatedAt;

    var affected = await context.Commits
                                .Where(c => c.Id == commitId)
                                .ExecuteUpdateAsync(
                                  s => s.SetProperty(static c => c.State, static c => c.State == CommitState.Known ? CommitState.Pending : c.State)
                                        .SetProperty(static c => c.Branch, c => c.Branch ?? branch)
                                        .SetProperty(static c => c.PullNumber, c => c.PullNumber ?? pullNumber)
                                        .SetProperty(static c => c.LastUploadAt, lastUploadAt)
                                        .SetProperty(static c => c.UpdatedAt, updatedAt),
                                  cancellationToken
                                )
                                .ConfigureAwait(false);

    if (affected == 0)
    {
      throw new ConflictException(GeneralErrors.ConcurrentChange, CommitGoneMessage);
    }

    // Written outside the tracker: a later save of this scope must not send the same change again.
    entry.State = EntityState.Unchanged;
  }

  /// <inheritdoc />
  /// <exception cref="ConflictException">The commit is not one of the organization of the scope.</exception>
  /// <remarks>
  ///   Portable equivalent of <c>UPDATE … RETURNING</c> / <c>OUTPUT</c>: the update holds the lock of the row until
  ///   the transaction ends, on both engines, so the value read right after it is this call's own and a
  ///   concurrent call waits for it. In a transaction of the caller, the row stays locked until that one ends.
  /// </remarks>
  public async Task<int> NextUploadSequenceAsync(long commitId, CancellationToken cancellationToken)
  {
    if (context.Database.CurrentTransaction is not null)
    {
      return await IncrementAsync(commitId, cancellationToken).ConfigureAwait(false);
    }

    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      var sequence = await IncrementAsync(commitId, cancellationToken).ConfigureAwait(false);
      await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

      return sequence;
    }
  }

  #endregion

  #region Methods

  /// <summary>Increments the session number of a commit and reads it back, within the current transaction.</summary>
  /// <param name="commitId">The identifier of the commit.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The new session number.</returns>
  /// <exception cref="ConflictException">The update affected no row.</exception>
  private async Task<int> IncrementAsync(long commitId, CancellationToken cancellationToken)
  {
    var updated = await context.Commits
                               .Where(c => c.Id == commitId)
                               .ExecuteUpdateAsync(static s => s.SetProperty(static c => c.UploadSequence, static c => c.UploadSequence + 1), cancellationToken)
                               .ConfigureAwait(false);

    if (updated == 0)
    {
      throw new ConflictException(GeneralErrors.ConcurrentChange, CommitGoneMessage);
    }

    return await context.Commits
                        .AsNoTracking()
                        .Where(c => c.Id == commitId)
                        .Select(static c => c.UploadSequence)
                        .SingleAsync(cancellationToken)
                        .ConfigureAwait(false);
  }

  #endregion
}
