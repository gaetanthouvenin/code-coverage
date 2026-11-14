// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettingsProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Infrastructure.Persistence;

namespace CodeCoverage.Infrastructure.Settings;

/// <summary><see cref="IOrganizationSettingsProvider" /> over the <c>OrganizationSettings</c> table.</summary>
/// <param name="context">The database context of the scope, whose query filter restricts the rows to its organization.</param>
/// <remarks>
///   <para>
///     Registered per scope, and the only cache of these values: a scope enters one organization once, so what it
///     has read cannot belong to another one. Nothing here may outlive the scope, or organizations would share it.
///   </para>
///   <para>
///     A read that fails, for example before the scope has entered an organization, is not remembered.
///   </para>
/// </remarks>
internal sealed class OrganizationSettingsProvider(CodeCoverageDbContext context) : IOrganizationSettingsProvider
{
  #region Fields

  /// <summary>The values read by this scope, or <see langword="null" /> before the first read.</summary>
  private OrganizationSettingsSnapshot? _snapshot;

  #endregion

  #region Methods

  /// <summary>Applies the precedence of the settings: the row of the organization, then the default of the catalog.</summary>
  /// <param name="rows">The saved values of the organization, by key.</param>
  /// <param name="key">The configuration key.</param>
  /// <returns>The effective value, or <see langword="null" /> when there is none.</returns>
  private static string? ValueOf(Dictionary<string, string> rows, string key)
  {
    return rows.TryGetValue(key, out var value) ? value : SettingDefinitions.Find(key)?.Default;
  }

  /// <summary>Builds the snapshot of an organization.</summary>
  /// <param name="rows">The saved values of the organization, by key.</param>
  /// <returns>The effective values.</returns>
  private static OrganizationSettingsSnapshot ToSnapshot(Dictionary<string, string> rows)
  {
    return new OrganizationSettingsSnapshot(
      ValueOf(rows, SettingDefinitions.NotificationsStatusContextPrefix)!,
      bool.Parse(ValueOf(rows, SettingDefinitions.NotificationsStatusesEnabled)!),
      bool.Parse(ValueOf(rows, SettingDefinitions.NotificationsCommentsEnabled)!),
      ValueOf(rows, SettingDefinitions.CoverageOrganizationYaml),
      bool.Parse(ValueOf(rows, SettingDefinitions.NotificationsChecksEnabled)!)
    );
  }

  #endregion

  #region Implementation of IOrganizationSettingsProvider

  /// <inheritdoc />
  public async Task<OrganizationSettingsSnapshot> GetAsync(CancellationToken cancellationToken)
  {
    if (_snapshot is not null)
    {
      return _snapshot;
    }

    var rows = await context.OrganizationSettings
                            .AsNoTracking()
                            .ToDictionaryAsync(static s => s.Key, static s => s.Value, StringComparer.OrdinalIgnoreCase, cancellationToken)
                            .ConfigureAwait(false);

    _snapshot = ToSnapshot(rows);

    return _snapshot;
  }

  #endregion
}
