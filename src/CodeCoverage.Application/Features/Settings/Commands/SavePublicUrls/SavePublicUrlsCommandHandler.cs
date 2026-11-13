// ------------------------------------------------------------------------------------------------
// <copyright file="SavePublicUrlsCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Commands.SavePublicUrls;

/// <summary>Handler for the <see cref="SavePublicUrlsCommand" /> command.</summary>
/// <param name="store">The settings store.</param>
/// <param name="issuer">The issuer of the setup token.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class SavePublicUrlsCommandHandler(
  ISettingsStore store,
  ISetupTokenIssuer issuer,
  IAuditLog auditLog) : ICommandHandler<SavePublicUrlsCommand>
{
  #region Fields

  /// <summary>The audit action of the public addresses saved during the setup.</summary>
  internal const string AuditAction = "Settings.SavePublicUrls";

  #endregion

  #region Implementation of ICommandHandler<SavePublicUrlsCommand,Unit>

  /// <inheritdoc />
  /// <exception cref="Exceptions.UnauthorizedException">The setup token is missing or invalid.</exception>
  public async ValueTask<Unit> Handle(SavePublicUrlsCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    SetupRules.ThrowIfInvalidToken(issuer, command.SetupToken);

    (string Key, string Value)[] values =
    [
      (SettingDefinitions.UrlsWebPublicUrl, command.WebPublicUrl),
      (SettingDefinitions.UrlsApiPublicUrl, command.ApiPublicUrl),
    ];

    foreach (var (key, value) in values)
    {
      var definition = SettingDefinitions.Find(key)!;

      await SettingRules.GetEditableAsync(store, definition, cancellationToken).ConfigureAwait(false);
      await store.SaveAsync(definition, SettingRules.Normalize(definition, value), cancellationToken).ConfigureAwait(false);
    }

    await auditLog.RecordAsync(AuditAction, "setup", null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
