// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteSetupCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Commands.CompleteSetup;

/// <summary>Handler for the <see cref="CompleteSetupCommand" /> command.</summary>
/// <param name="store">The settings store.</param>
/// <param name="issuer">The issuer of the setup token.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class CompleteSetupCommandHandler(
  ISettingsStore store,
  ISetupTokenIssuer issuer,
  IAuditLog auditLog) : ICommandHandler<CompleteSetupCommand>
{
  #region Fields

  /// <summary>The audit action of a finished setup.</summary>
  internal const string AuditAction = "Settings.CompleteSetup";

  private const string AppNotCreatedMessage = "The setup cannot end before the GitHub App exists.";

  #endregion

  #region Methods

  /// <summary>Lists the values given by a command, with their settings.</summary>
  /// <param name="command">The command.</param>
  /// <returns>The settings to write and their values.</returns>
  private static List<(SettingDefinition Definition, string Value)> ValuesOf(CompleteSetupCommand command)
  {
    (string Key, string? Value)[] given =
    [
      (SettingDefinitions.UrlsWebPublicUrl, command.WebPublicUrl),
      (SettingDefinitions.UrlsApiPublicUrl, command.ApiPublicUrl),
      (SettingDefinitions.AccessAdminUserIds, command.AdminUserIds),
    ];

    return [.. given.Where(static v => v.Value is not null).Select(static v => (SettingDefinitions.Find(v.Key)!, v.Value!))];
  }

  #endregion

  #region Implementation of ICommandHandler<CompleteSetupCommand,Unit>

  /// <inheritdoc />
  /// <remarks>Every setting is checked before the first is saved, and the token is forgotten only once all of them are.</remarks>
  /// <exception cref="UnauthorizedException">The token is not valid.</exception>
  /// <exception cref="UnprocessableEntityException">
  ///   The App does not exist (<see cref="SettingErrors.GitHubAppNotCreated" />), or a stronger source provides a value.
  /// </exception>
  public async ValueTask<Unit> Handle(CompleteSetupCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    SetupRules.ThrowIfInvalidToken(issuer, command.SetupToken);

    if (!await SetupRules.IsAppCreatedAsync(store, cancellationToken).ConfigureAwait(false))
    {
      throw new UnprocessableEntityException(SettingErrors.GitHubAppNotCreated, AppNotCreatedMessage);
    }

    var values = ValuesOf(command);

    foreach (var (definition, _) in values)
    {
      await SettingRules.GetEditableAsync(store, definition, cancellationToken).ConfigureAwait(false);
    }

    foreach (var (definition, value) in values)
    {
      await store.SaveAsync(definition, SettingRules.Normalize(definition, value), cancellationToken).ConfigureAwait(false);
    }

    await auditLog.RecordAsync(AuditAction, "setup", null, cancellationToken).ConfigureAwait(false);

    issuer.Revoke();

    return Unit.Value;
  }

  #endregion
}
