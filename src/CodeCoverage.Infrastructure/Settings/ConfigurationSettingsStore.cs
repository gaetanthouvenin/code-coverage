// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationSettingsStore.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>
///   <see cref="ISettingsStore" /> over the running configuration (for the effective value and its
///   source) and the <c>Settings</c> table (for the saved values and who saved them).
/// </summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="configuration">The configuration of the host, refreshed after each save.</param>
/// <param name="currentUser">The principal of the scope, recorded as the author of a change.</param>
/// <param name="clock">The clock that dates the changes.</param>
/// <remarks>
///   <para>
///     A save writes the value <b>and</b> runs <c>UPDATE SettingsRevision SET Revision = Revision + 1</c>
///     in one transaction, so that the other hosts see the change on their next poll; the host that
///     saves refreshes its configuration at once (<see cref="DatabaseSettingsConfigurationProvider.RefreshAll" />).
///   </para>
///   <para>
///     The saved row is detached once written, whatever the outcome, so that the context of the scope
///     never flushes it again in a later save.
///   </para>
/// </remarks>
internal sealed class ConfigurationSettingsStore(
  CodeCoverageDbContext context,
  IConfiguration configuration,
  ICurrentUser currentUser,
  IClock clock) : ISettingsStore
{
  #region Methods

  /// <summary>Tells which source provides the effective value of a setting.</summary>
  /// <param name="definition">The setting.</param>
  /// <returns>The source of the effective value.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="definition" /> is <see langword="null" />.</exception>
  internal SettingSource GetSource(SettingDefinition definition)
  {
    ArgumentNullException.ThrowIfNull(definition);

    return ConfigurationSourceInspector.Resolve(configuration, definition.Key).Source;
  }

  /// <summary>Builds the effective value of a setting from the configuration and its saved row.</summary>
  /// <param name="definition">The setting.</param>
  /// <param name="saved">The saved row, or <see langword="null" /> when the store holds no value.</param>
  /// <returns>The effective value.</returns>
  private SettingValue ToValue(SettingDefinition definition, StoredSetting? saved)
  {
    var (source, value) = ConfigurationSourceInspector.Resolve(configuration, definition.Key);

    return SettingValue.Create(
      definition,
      source == SettingSource.Default ? definition.Default : value,
      source,
      saved?.UpdatedAt,
      saved?.UpdatedBy
    );
  }

  /// <summary>Increments the settings revision, in the current transaction.</summary>
  /// <param name="cancellationToken">A token to cancel the update.</param>
  /// <returns>A task that completes once the revision is incremented.</returns>
  /// <exception cref="InvalidOperationException">The revision row is missing.</exception>
  private async Task IncrementRevisionAsync(CancellationToken cancellationToken)
  {
    var updated = await context.SettingsRevisions
                               .Where(static r => r.Id == SettingsRevisionRow.SingletonId)
                               .ExecuteUpdateAsync(static s => s.SetProperty(r => r.Revision, r => r.Revision + 1), cancellationToken)
                               .ConfigureAwait(false);

    InvalidOperationException.ThrowIf(
      updated != 1,
      "The SettingsRevision row is missing: the AddSettings migration seeds it, the database is not migrated."
    );
  }

  /// <summary>Applies the saved change to the running configuration of this host.</summary>
  private void RefreshConfiguration()
  {
    // IOptionsMonitor consumers are notified; the other hosts follow on their next poll.
    if (configuration is IConfigurationRoot root)
    {
      DatabaseSettingsConfigurationProvider.RefreshAll(root);
    }
  }

  #endregion

  #region Implementation of ISettingsStore

  /// <inheritdoc />
  public async Task<SettingValue> GetAsync(SettingDefinition definition, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(definition);

    var saved = await context.Settings
                             .AsNoTracking()
                             .Where(s => s.Key == definition.Key)
                             .Select(static s => new StoredSetting { Key = s.Key, UpdatedAt = s.UpdatedAt, UpdatedBy = s.UpdatedBy })
                             .FirstOrDefaultAsync(cancellationToken)
                             .ConfigureAwait(false);

    return ToValue(definition, saved);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<SettingValue>> ListAsync(CancellationToken cancellationToken)
  {
    var saved = await context.Settings
                             .AsNoTracking()
                             .Select(static s => new StoredSetting { Key = s.Key, UpdatedAt = s.UpdatedAt, UpdatedBy = s.UpdatedBy })
                             .ToDictionaryAsync(static s => s.Key, StringComparer.OrdinalIgnoreCase, cancellationToken)
                             .ConfigureAwait(false);

    return [.. SettingDefinitions.All.Select(d => ToValue(d, saved.GetValueOrDefault(d.Key)))];
  }

  /// <inheritdoc />
  public async Task SaveAsync(SettingDefinition definition, string value, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(definition);
    ArgumentNullException.ThrowIfNull(value);

    StoredSetting? saved = null;
    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      try
      {
        saved = await context.Settings
                             .FirstOrDefaultAsync(s => s.Key == definition.Key, cancellationToken)
                             .ConfigureAwait(false);

        var isNew = saved is null;

        if (saved is null)
        {
          saved = new StoredSetting { Key = definition.Key, UpdatedBy = currentUser.GetActorName() };
          context.Settings.Add(saved);
        }

        saved.Value = value;
        saved.IsSecret = definition.IsSecret;
        saved.UpdatedAt = clock.UtcNow;
        saved.UpdatedBy = currentUser.GetActorName();

        try
        {
          await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (isNew)
        {
          // Another request inserted the same key between the read and the insert: the primary key refuses it.
          throw new ConflictException(
            "Settings.ConcurrentSave",
            $"The setting '{definition.Key}' was saved by another request at the same time; reload it and save again.",
            exception
          );
        }

        await IncrementRevisionAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }
      finally
      {
        if (saved is not null)
        {
          context.Entry(saved).State = EntityState.Detached;
        }
      }
    }

    RefreshConfiguration();
  }

  /// <inheritdoc />
  public async Task ResetAsync(SettingDefinition definition, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(definition);

    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      var removed = await context.Settings
                                 .Where(s => s.Key == definition.Key)
                                 .ExecuteDeleteAsync(cancellationToken)
                                 .ConfigureAwait(false);

      if (removed == 0)
      {
        // Nothing to reset: the transaction is rolled back and the revision is left as it is.
        return;
      }

      await IncrementRevisionAsync(cancellationToken).ConfigureAwait(false);
      await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    RefreshConfiguration();
  }

  #endregion
}
