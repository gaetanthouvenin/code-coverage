// ------------------------------------------------------------------------------------------------
// <copyright file="SqlAuditTrail.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Audit;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Operations;

/// <summary>Reads the audit log of the instance from the operational table <c>AuditEntries</c>.</summary>
/// <param name="context">The database.</param>
internal sealed class SqlAuditTrail(CodeCoverageDbContext context) : IAuditReader
{
  #region Methods

  // The same families as AuditCategories.Of, written so that both engines translate them.
  private static IQueryable<AuditEntry> Filter(IQueryable<AuditEntry> entries, AuditCategory? category)
  {
    var secretKeys = AuditCategories.SecretKeys.ToList();

    return category switch
    {
      AuditCategory.Secrets => entries.Where(e => e.Action == AuditCategories.ReplaceSecretAction
                                                  || (e.Action == AuditCategories.ResetSettingAction && secretKeys.Contains(e.Target))),
      AuditCategory.Settings => entries.Where(e => e.Action.StartsWith(AuditCategories.SettingsPrefix)
                                                   && e.Action != AuditCategories.ReplaceSecretAction
                                                   && !(e.Action == AuditCategories.ResetSettingAction && secretKeys.Contains(e.Target))),
      AuditCategory.Organizations => entries.Where(static e => e.Action.StartsWith(AuditCategories.OrganizationsPrefix)),
      AuditCategory.Tokens => entries.Where(static e => e.Action.StartsWith(AuditCategories.TokensPrefix)),
      AuditCategory.Retries => entries.Where(static e => e.Action == AuditCategories.RetryJobAction),
      var _ => entries,
    };
  }

  #endregion

  #region Implementation of IAuditReader

  /// <inheritdoc />
  public async Task<AuditEntryPage> ListInstanceAsync(AuditCategory? category, string? search, int page, int pageSize, CancellationToken cancellationToken)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

    // The rows of the instance only, chosen explicitly: the table is operational, no query filter applies to it.
    var entries = context.AuditEntries
                         .AsNoTracking()
                         .Where(static e => EF.Property<long?>(e, OrganizationOwnedExtensions.ColumnName) == null);

    // Fail-closed: an event of a token bound to an organization, recorded before those events went to its organization.
    entries = entries.Where(e => !(e.Action.StartsWith(AuditCategories.TokensPrefix)
                                   && context.ApiTokens.Any(t => t.OrganizationId != null && t.Id.ToString() == e.Target)));
    entries = Filter(entries, category);

    if (!string.IsNullOrWhiteSpace(search))
    {
      // Lower case on both sides: the same answer on both engines, whatever their collation.
      var term = search.Trim().ToLowerInvariant();
      entries = entries.Where(e => e.Actor.ToLower().Contains(term) || e.Action.ToLower().Contains(term) || e.Target.ToLower().Contains(term));
    }

    var total = await entries.CountAsync(cancellationToken).ConfigureAwait(false);
    var rows = await entries.OrderByDescending(static e => e.At)
                            .ThenByDescending(static e => e.Id)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .Select(static e => new { e.Id, e.At, e.Actor, e.Action, e.Target })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

    return new AuditEntryPage(
      [.. rows.Select(static r => new AuditEntrySummary(r.Id, r.At, r.Actor, r.Action, r.Target, AuditCategories.Of(r.Action, r.Target)))],
      page,
      pageSize,
      total
    );
  }

  #endregion
}
