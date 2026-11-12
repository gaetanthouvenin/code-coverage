// ------------------------------------------------------------------------------------------------
// <copyright file="GetOrganizationSettingsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Organizations.Queries.GetOrganizationSettings;

/// <summary>Handler for the <see cref="GetOrganizationSettingsQuery" /> query.</summary>
/// <param name="settings">The settings stored for the organization of the scope.</param>
/// <remarks>It reads the table, never the provider memoized for the scope, so that a value saved just before is seen.</remarks>
internal sealed class GetOrganizationSettingsQueryHandler(IOrganizationSettingRepository settings)
  : IQueryHandler<GetOrganizationSettingsQuery, IReadOnlyList<SettingValue>>
{
  #region Implementation of IQueryHandler<GetOrganizationSettingsQuery,IReadOnlyList<SettingValue>>

  /// <inheritdoc />
  public async ValueTask<IReadOnlyList<SettingValue>> Handle(GetOrganizationSettingsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var stored = (await settings.ListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(static v => v.Key, StringComparer.Ordinal);

    return
    [
      .. SettingDefinitions.All
                           .Where(static d => d.Scope is SettingScope.Organization)
                           .OrderBy(static d => d.Key, StringComparer.Ordinal)
                           .Select(d => stored.TryGetValue(d.Key, out var value) ? value : SettingValue.Create(d, null, SettingSource.Default, null, null)),
    ];
  }

  #endregion
}
