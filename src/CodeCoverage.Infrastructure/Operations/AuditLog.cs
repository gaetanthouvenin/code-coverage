// ------------------------------------------------------------------------------------------------
// <copyright file="AuditLog.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Operations;

/// <summary>Saves the audit trail in the <c>AuditEntries</c> table.</summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="currentUser">The principal of the scope, recorded as the actor.</param>
/// <param name="currentOrganization">The organization of the scope, if any, that the entry is attributed to.</param>
/// <param name="clock">The clock that dates the entries.</param>
/// <remarks>
///   <para>
///     The organization always comes from the scope, never from the caller; an action outside any organization
///     stores none.
///   </para>
///   <para>
///     The entry is saved at once, through the context of the scope: as every repository saves its own
///     changes immediately, the context holds no other pending change at that point. This is a known
///     constraint of the shared context, not enforced here: a change left pending by the caller would be
///     flushed by the same save.
///   </para>
///   <para>
///     When the save fails (database error, cancellation), the entry is detached before the exception is
///     rethrown, so a later save in the same scope neither inserts it silently nor fails on it again.
///   </para>
/// </remarks>
internal sealed class AuditLog(
  CodeCoverageDbContext context,
  ICurrentUser currentUser,
  ICurrentOrganization currentOrganization,
  IClock clock) : IAuditLog
{
  #region Methods

  private async Task SaveAsync(long? organizationId, string action, string target, IReadOnlyDictionary<string, string>? details, CancellationToken cancellationToken)
  {
    var entry = AuditEntry.Create(clock.UtcNow, currentUser.GetActorName(), action, target, details);

    context.AuditEntries.Add(entry).Property<long?>(OrganizationOwnedExtensions.ColumnName).CurrentValue = organizationId;

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch
    {
      context.Entry(entry).State = EntityState.Detached;
      throw;
    }
  }

  #endregion

  #region Implementation of IAuditLog

  /// <inheritdoc />
  public async Task RecordAsync(
    string action,
    string target,
    IReadOnlyDictionary<string, string>? details,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(action);
    ArgumentNullException.ThrowIfNull(target);

    await SaveAsync(currentOrganization.IsKnown ? currentOrganization.Id : null, action, target, details, cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task RecordAsync(
    long organizationId,
    string action,
    string target,
    IReadOnlyDictionary<string, string>? details,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(action);
    ArgumentNullException.ThrowIfNull(target);

    await SaveAsync(organizationId, action, target, details, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
