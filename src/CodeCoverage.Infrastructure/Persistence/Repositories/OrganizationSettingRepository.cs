// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettingRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Persistence.Configurations;
using CodeCoverage.Infrastructure.Settings;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>OrganizationSettings</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>
///   A row is added with the organization of the scope, stamped by the context. Two first writes of the same key at
///   once meet the unique index: the second is reported as a concurrent change, never as a server error.
/// </remarks>
internal sealed class OrganizationSettingRepository(CodeCoverageDbContext context) : IOrganizationSettingRepository
{
  #region Implementation of IOrganizationSettingRepository

  /// <inheritdoc />
  public async Task<IReadOnlyList<SettingValue>> ListAsync(CancellationToken cancellationToken)
  {
    var rows = await context.OrganizationSettings
                            .AsNoTracking()
                            .OrderBy(static s => s.Key)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

    return
    [
      .. rows.Select(static row => (Row: row, Definition: SettingDefinitions.Find(row.Key)))
             .Where(static r => r.Definition is { Scope: SettingScope.Organization })
             .Select(static r => SettingValue.Create(r.Definition!, r.Row.Value, SettingSource.Store, r.Row.UpdatedAt, r.Row.UpdatedBy)),
    ];
  }

  /// <inheritdoc />
  /// <exception cref="ConflictException">Another write of the same key saved first (<see cref="GeneralErrors.ConcurrentChange" />).</exception>
  public async Task SaveAsync(SettingDefinition definition, string value, string updatedBy, DateTimeOffset at, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(definition);
    ArgumentNullException.ThrowIfNull(value);
    ArgumentNullException.ThrowIfNull(updatedBy);

    var row = await context.OrganizationSettings.SingleOrDefaultAsync(s => s.Key == definition.Key, cancellationToken).ConfigureAwait(false);

    if (row is null)
    {
      row = new StoredOrganizationSetting { Key = definition.Key, Value = value, UpdatedBy = updatedBy };
      context.OrganizationSettings.Add(row);
    }

    row.Value = value;
    row.UpdatedAt = at.ToUniversalTime();
    row.UpdatedBy = updatedBy;

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, OrganizationSettingConfiguration.KeyIndexName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(row).State = EntityState.Detached;

      throw new ConflictException(GeneralErrors.ConcurrentChange, "The setting was changed at the same time.", exception);
    }
  }

  /// <inheritdoc />
  public async Task<bool> RemoveAsync(SettingDefinition definition, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(definition);

    var removed = await context.OrganizationSettings
                               .Where(s => s.Key == definition.Key)
                               .ExecuteDeleteAsync(cancellationToken)
                               .ConfigureAwait(false);

    return removed > 0;
  }

  #endregion
}
