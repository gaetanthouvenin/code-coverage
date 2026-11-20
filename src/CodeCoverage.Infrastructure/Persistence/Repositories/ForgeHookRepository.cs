// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>ForgeHooks</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>
///   A hook is added with the organization of the scope, stamped by the context. The state of a known scope is
///   replaced by one statement, which needs no read first. Two first writes of the same scope at once meet the unique
///   index: the second is reported as a concurrent change, never as a server error.
/// </remarks>
internal sealed class ForgeHookRepository(CodeCoverageDbContext context) : IForgeHookRepository
{
  #region Implementation of IForgeHookRepository

  /// <inheritdoc />
  public async Task<IReadOnlyList<ForgeHook>> ListAsync(string connectionId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(connectionId);

    return await context.ForgeHooks
                        .AsNoTracking()
                        .Where(h => h.ConnectionId == connectionId)
                        .OrderBy(static h => h.ScopeKind)
                        .ThenBy(static h => h.ScopeExternalId)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="ConflictException">
  ///   Another write of the same scope added it first (<see cref="GeneralErrors.ConcurrentChange" />).
  /// </exception>
  public async Task UpsertAsync(ForgeHook hook, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(hook);

    var connectionId = hook.ConnectionId;
    var scopeKind = hook.ScopeKind;
    var scopeExternalId = hook.ScopeExternalId;
    var externalId = hook.ExternalId;
    var state = hook.State;
    var lastCheckedAt = hook.LastCheckedAt;
    var lastError = hook.LastError;

    var updated = await context.ForgeHooks
                               .Where(h => h.ConnectionId == connectionId && h.ScopeKind == scopeKind && h.ScopeExternalId == scopeExternalId)
                               .ExecuteUpdateAsync(
                                 setters => setters.SetProperty(static h => h.ExternalId, externalId)
                                                   .SetProperty(static h => h.State, state)
                                                   .SetProperty(static h => h.LastCheckedAt, lastCheckedAt)
                                                   .SetProperty(static h => h.LastError, lastError),
                                 cancellationToken)
                               .ConfigureAwait(false);

    if (updated > 0)
    {
      return;
    }

    context.ForgeHooks.Add(hook);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, ForgeHookConfiguration.ScopeIndexName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(hook).State = EntityState.Detached;

      throw ConflictException.ConcurrentChange("The hook of this scope was saved at the same time.", exception);
    }
  }

  #endregion
}
