// ------------------------------------------------------------------------------------------------
// <copyright file="SaveGitHubAppCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Commands.SaveGitHubApp;

/// <summary>Handler for the <see cref="SaveGitHubAppCommand" /> command.</summary>
/// <param name="store">The settings store.</param>
/// <param name="issuer">The issuer of the setup token.</param>
/// <param name="protector">Protects the secrets before they are stored.</param>
/// <param name="auditLog">The audit trail.</param>
/// <remarks>
///   The identifier of the App is saved last, as the manifest handler does: it is what tells the instance the setup
///   went through, so nothing claims an App whose secrets are not stored yet.
/// </remarks>
internal sealed class SaveGitHubAppCommandHandler(
  ISettingsStore store,
  ISetupTokenIssuer issuer,
  ISecretProtector protector,
  IAuditLog auditLog) : ICommandHandler<SaveGitHubAppCommand>
{
  #region Fields

  /// <summary>The audit action of an App entered by hand.</summary>
  internal const string AuditAction = "Settings.SaveGitHubApp";

  #endregion

  #region Methods

  private Task SaveAsync(string key, string value, CancellationToken cancellationToken) =>
    store.SaveAsync(SettingDefinitions.Find(key)!, value, cancellationToken);

  private Task SaveSecretAsync(string key, string secret, CancellationToken cancellationToken) =>
    SaveAsync(key, protector.Protect(secret), cancellationToken);

  #endregion

  #region Implementation of ICommandHandler<SaveGitHubAppCommand,Unit>

  /// <inheritdoc />
  /// <exception cref="UnauthorizedException">The setup token is missing or invalid.</exception>
  /// <exception cref="ConflictException">The instance already has a GitHub App.</exception>
  public async ValueTask<Unit> Handle(SaveGitHubAppCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    SetupRules.ThrowIfInvalidToken(issuer, command.SetupToken);

    ConflictException.ThrowIfGitHubAppAlreadyCreated(await SetupRules.IsAppCreatedAsync(store, cancellationToken).ConfigureAwait(false));

    await SaveAsync(SettingDefinitions.GitHubClientId, command.ClientId, cancellationToken).ConfigureAwait(false);

    if (command.Slug is { Length: > 0 } slug)
    {
      await SaveAsync(SettingDefinitions.GitHubAppSlug, slug, cancellationToken).ConfigureAwait(false);
    }

    await SaveSecretAsync(SettingDefinitions.GitHubClientSecret, command.ClientSecret, cancellationToken).ConfigureAwait(false);
    await SaveSecretAsync(SettingDefinitions.GitHubWebhookSecret, command.WebhookSecret, cancellationToken).ConfigureAwait(false);
    await SaveSecretAsync(SettingDefinitions.GitHubPrivateKeyPem, command.PrivateKeyPem, cancellationToken).ConfigureAwait(false);
    await SaveAsync(SettingDefinitions.GitHubAppId, command.AppId, cancellationToken).ConfigureAwait(false);

    await auditLog.RecordAsync(AuditAction, command.AppId, null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
