// ------------------------------------------------------------------------------------------------
// <copyright file="BranchRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>Branches</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>
///   <see cref="FindAsync" /> tracks what it returns: the shadow column of the organization does not travel with
///   an instance, so a row can only be written by the context that read it (ADR-013), and the original
///   <c>Version</c> it holds is what detects a concurrent write.
/// </remarks>
internal sealed class BranchRepository(CodeCoverageDbContext context) : IBranchRepository
{
  #region Fields

  /// <summary>The message of the refusal of a branch added by another save meanwhile; it is sent to the caller.</summary>
  private const string DuplicateMessage = "The branch was added in the meantime; reload it and try again.";

  /// <summary>The message of the refusal of an instance that this context did not read.</summary>
  private const string NotReadMessage = "A branch must be read in this scope before it can be updated.";

  /// <summary>The message of the refusal of an unknown repository; it is sent to the caller.</summary>
  private const string RepositoryNotFoundMessage = "The repository does not exist or cannot be read.";

  #endregion

  #region Implementation of IBranchRepository

  /// <inheritdoc />
  public async Task<Branch?> FindAsync(long repositoryId, string name, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(name);

    return await context.Branches
                        .SingleOrDefaultAsync(b => b.RepositoryId == repositoryId && b.Name == name, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<Branch>> ListAsync(long repositoryId, CancellationToken cancellationToken) =>
    await context.Branches
                 .AsNoTracking()
                 .Where(b => b.RepositoryId == repositoryId)
                 .OrderBy(static b => b.Name)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  /// <exception cref="NotFoundException">The repository is not one of the organization of the scope.</exception>
  /// <exception cref="ConflictException">A concurrent save added the same branch.</exception>
  /// <remarks>
  ///   The primary key does not hold the organization, so a row written for the repository of another
  ///   organization would take its branch name there: the repository is read first, under the filter.
  /// </remarks>
  public async Task AddAsync(Branch branch, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(branch);

    var repositoryId = branch.RepositoryId;
    var known = await context.Repositories.AnyAsync(r => r.Id == repositoryId, cancellationToken).ConfigureAwait(false);

    if (!known)
    {
      throw new NotFoundException(RepositoryErrors.NotFound, RepositoryNotFoundMessage);
    }

    context.Branches.Add(branch);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, BranchConfiguration.PrimaryKeyName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(branch).State = EntityState.Detached;

      throw new ConflictException(GeneralErrors.ConcurrentChange, DuplicateMessage, exception);
    }
  }

  /// <inheritdoc />
  public async Task UpdateAsync(Branch branch, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(branch);

    InvalidOperationException.ThrowIf(context.Entry(branch).State is EntityState.Detached, NotReadMessage);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task RemoveAsync(long repositoryId, string name, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(name);

    var branch = await FindAsync(repositoryId, name, cancellationToken).ConfigureAwait(false);

    if (branch is null)
    {
      return;
    }

    context.Branches.Remove(branch);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
