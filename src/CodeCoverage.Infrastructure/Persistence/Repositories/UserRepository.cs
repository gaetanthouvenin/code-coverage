// ------------------------------------------------------------------------------------------------
// <copyright file="UserRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>Users</c> table, which is global.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>The readings track what they return: the original <c>Version</c> a row holds is what detects a concurrent write.</remarks>
internal sealed class UserRepository(CodeCoverageDbContext context) : IUserRepository
{
  #region Fields

  /// <summary>The message of the refusal of a user that this context did not read.</summary>
  private const string NotReadMessage = "A user must be read in this scope before it can be updated.";

  /// <summary>The message of the refusal of a change that lost a race; it is sent to the caller.</summary>
  private const string ConcurrentChangeMessage = "The user was changed in the meantime; reload it and try again.";

  #endregion

  #region Implementation of IUserRepository

  /// <inheritdoc />
  public async Task<AppUser?> FindAsync(long id, CancellationToken cancellationToken) =>
    await context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<AppUser?> FindByExternalIdAsync(Forge forge, string externalId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(externalId);

    var normalized = Organization.NormalizeExternalAccountId(externalId);

    return await context.Users.SingleOrDefaultAsync(u => u.Forge == forge && u.ExternalId == normalized, cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<bool> TryAddAsync(AppUser user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    context.Users.Add(user);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

      return true;
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, AppUserConfiguration.AccountIndexName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(user).State = EntityState.Detached;

      return false;
    }
  }

  /// <inheritdoc />
  /// <exception cref="ConflictException">Another save changed the user in the meantime.</exception>
  public async Task UpdateAsync(AppUser user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    InvalidOperationException.ThrowIf(context.Entry(user).State is EntityState.Detached, NotReadMessage);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateConcurrencyException exception)
    {
      // The stale row is not kept: a later save of the scope would fail the same way.
      context.Entry(user).State = EntityState.Detached;

      throw new ConflictException(GeneralErrors.ConcurrentChange, ConcurrentChangeMessage, exception);
    }
  }

  #endregion
}
