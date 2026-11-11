// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>PullRequests</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>
///   <see cref="FindOpenByHeadShaAsync" /> and <see cref="UpsertAsync" /> track what they return: a row can only be written
///   by the context that read it (ADR-013), and the original <c>Version</c> it holds is what detects a concurrent write.
/// </remarks>
internal sealed class PullRequestRepository(CodeCoverageDbContext context) : IPullRequestRepository
{
  #region Fields

  /// <summary>The message of the refusal of an unknown repository; it is sent to the caller.</summary>
  private const string RepositoryNotFoundMessage = "The repository does not exist or cannot be read.";

  /// <summary>The message of the refusal of an instance that this scope did not read.</summary>
  private const string NotReadMessage = "A pull request must be read in this scope before it can be updated.";

  /// <summary>The message of the refusal of a pull request that cannot be read; it is sent to the caller.</summary>
  private const string PullRequestGoneMessage = "The pull request was changed or removed in the meantime; reload it and try again.";

  #endregion

  #region Implementation of IPullRequestRepository

  /// <inheritdoc />
  public async Task<PullRequest?> FindOpenByHeadShaAsync(long repositoryId, GitSha headSha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(headSha);

    return await context.PullRequests
                        .Where(p => p.RepositoryId == repositoryId && p.HeadSha == headSha && p.State == PullRequestState.Open)
                        .OrderByDescending(static p => p.Number)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<PullRequest>> ListOpenByForkPointAsync(long repositoryId, GitSha forkPointSha, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(forkPointSha);

    return await context.PullRequests
                        .AsNoTracking()
                        .Where(p => p.RepositoryId == repositoryId && p.ForkPointSha == forkPointSha && p.State == PullRequestState.Open)
                        .OrderBy(static p => p.Number)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository is not one of the organization of the scope.</exception>
  /// <exception cref="ConflictException">The row that refused the insertion can no longer be read, or changed in the meantime.</exception>
  /// <remarks>
  ///   The unique index does not hold the organization, so the repository is read first, under the filter. A concurrent
  ///   call that inserted the row first makes the insertion fail on the index: the row is then read and updated.
  /// </remarks>
  public async Task<PullRequest> UpsertAsync(long repositoryId, ForgePullInfo pull, DateTimeOffset at, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(pull);

    var known = await context.Repositories.AnyAsync(r => r.Id == repositoryId, cancellationToken).ConfigureAwait(false);

    if (!known)
    {
      throw new NotFoundException(RepositoryErrors.NotFound, RepositoryNotFoundMessage);
    }

    var existing = await FindByNumberAsync(repositoryId, pull.Number, cancellationToken).ConfigureAwait(false);

    if (existing is not null)
    {
      return await RefreshAsync(existing, pull, at, cancellationToken).ConfigureAwait(false);
    }

    var added = PullRequest.Create(repositoryId, pull, at);
    context.PullRequests.Add(added);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

      return added;
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, PullRequestConfiguration.NumberIndexName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(added).State = EntityState.Detached;

      var winner = await FindByNumberAsync(repositoryId, pull.Number, cancellationToken).ConfigureAwait(false)
                   ?? throw new ConflictException(GeneralErrors.ConcurrentChange, PullRequestGoneMessage, exception);

      return await RefreshAsync(winner, pull, at, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <inheritdoc />
  public async Task UpdateAsync(PullRequest pullRequest, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(pullRequest);

    InvalidOperationException.ThrowIf(context.Entry(pullRequest).State is EntityState.Detached, NotReadMessage);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange)
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(pullRequest).State = EntityState.Detached;

      throw;
    }
  }

  #endregion

  #region Methods

  /// <summary>Reads a pull request by its number.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The pull request, or <see langword="null" />.</returns>
  private async Task<PullRequest?> FindByNumberAsync(long repositoryId, int number, CancellationToken cancellationToken) =>
    await context.PullRequests
                 .SingleOrDefaultAsync(p => p.RepositoryId == repositoryId && p.Number == number, cancellationToken)
                 .ConfigureAwait(false);

  /// <summary>Applies what the forge reports to a row read in this scope, and saves it.</summary>
  /// <param name="pullRequest">The pull request.</param>
  /// <param name="pull">What the forge reports.</param>
  /// <param name="at">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The pull request.</returns>
  private async Task<PullRequest> RefreshAsync(PullRequest pullRequest, ForgePullInfo pull, DateTimeOffset at, CancellationToken cancellationToken)
  {
    pullRequest.Refresh(pull, at);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    return pullRequest;
  }

  #endregion
}
