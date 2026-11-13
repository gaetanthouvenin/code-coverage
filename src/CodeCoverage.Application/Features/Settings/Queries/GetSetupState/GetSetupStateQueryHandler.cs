// ------------------------------------------------------------------------------------------------
// <copyright file="GetSetupStateQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Queries.GetSetupState;

/// <summary>Handler for the <see cref="GetSetupStateQuery" /> query.</summary>
/// <param name="store">The settings store.</param>
/// <param name="issuer">The issuer of the setup token.</param>
internal sealed class GetSetupStateQueryHandler(ISettingsStore store, ISetupTokenIssuer issuer) : IQueryHandler<GetSetupStateQuery, SetupState>
{
  #region Methods

  /// <summary>Tells whether a setting has a value.</summary>
  /// <param name="key">The key of the setting.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns><see langword="true" /> when the value is not empty.</returns>
  private async Task<bool> HasValueAsync(string key, CancellationToken cancellationToken)
  {
    return await ValueOfAsync(key, cancellationToken).ConfigureAwait(false) is not null;
  }

  private async Task<string?> ValueOfAsync(string key, CancellationToken cancellationToken)
  {
    var value = await store.GetAsync(SettingDefinitions.Find(key)!, cancellationToken).ConfigureAwait(false);

    return string.IsNullOrWhiteSpace(value.Value) ? null : value.Value;
  }

  #endregion

  #region Implementation of IQueryHandler<GetSetupStateQuery,SetupState>

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">The token is not valid.</exception>
  public async ValueTask<SetupState> Handle(GetSetupStateQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    SetupRules.ThrowIfInvalidToken(issuer, query.SetupToken);

    var app = await SetupRules.IsAppCreatedAsync(store, cancellationToken).ConfigureAwait(false);
    var web = await ValueOfAsync(SettingDefinitions.UrlsWebPublicUrl, cancellationToken).ConfigureAwait(false);
    var api = await ValueOfAsync(SettingDefinitions.UrlsApiPublicUrl, cancellationToken).ConfigureAwait(false);
    var administrators = await HasValueAsync(SettingDefinitions.AccessAdminUserIds, cancellationToken).ConfigureAwait(false);

    return new SetupState(app, web is not null && api is not null, administrators, web, api);
  }

  #endregion
}
