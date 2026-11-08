// ------------------------------------------------------------------------------------------------
// <copyright file="GetSettingsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Queries.GetSettings;

/// <summary>Handler for the <see cref="GetSettingsQuery" /> query.</summary>
/// <param name="store">The settings store.</param>
internal sealed class GetSettingsQueryHandler(ISettingsStore store)
  : IQueryHandler<GetSettingsQuery, IReadOnlyList<SettingValue>>
{
  #region Implementation of IQueryHandler<GetSettingsQuery,IReadOnlyList<SettingValue>>

  /// <inheritdoc />
  /// <remarks>Every value passes through <see cref="SettingValue.Redact" />: no secret leaves, even protected.</remarks>
  public async ValueTask<IReadOnlyList<SettingValue>> Handle(
    GetSettingsQuery query,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var values = await store.ListAsync(cancellationToken).ConfigureAwait(false);

    return [.. values.Select(v => v.Redact())];
  }

  #endregion
}
