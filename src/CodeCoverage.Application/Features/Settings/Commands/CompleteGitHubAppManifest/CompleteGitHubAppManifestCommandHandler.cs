// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteGitHubAppManifestCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Commands.CompleteGitHubAppManifest;

/// <summary>Handler for the <see cref="CompleteGitHubAppManifestCommand" /> command.</summary>
/// <param name="store">The settings store.</param>
/// <param name="issuer">The issuer of the setup token.</param>
/// <param name="gateway">The gateway of the GitHub App.</param>
/// <param name="protector">The protector of the secrets.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class CompleteGitHubAppManifestCommandHandler(
  ISettingsStore store,
  ISetupTokenIssuer issuer,
  IGitHubAppGateway gateway,
  ISecretProtector protector,
  IAuditLog auditLog) : ICommandHandler<CompleteGitHubAppManifestCommand>
{
  #region Fields

  /// <summary>The audit action of an App created from a manifest.</summary>
  internal const string AuditAction = "Settings.CompleteGitHubAppManifest";

  private const string AlreadyCreatedMessage = "The GitHub App already exists on this instance.";

  private const string InvalidCodeMessage = "GitHub does not know this code, or it has expired.";

  private static readonly string[] WrittenKeys =
  [
    SettingDefinitions.GitHubClientId,
    SettingDefinitions.GitHubAppSlug,
    SettingDefinitions.GitHubClientSecret,
    SettingDefinitions.GitHubWebhookSecret,
    SettingDefinitions.GitHubPrivateKeyPem,
    SettingDefinitions.GitHubAppId,
  ];

  #endregion

  #region Methods

  /// <summary>Saves a value in clear.</summary>
  /// <param name="key">The key of the setting.</param>
  /// <param name="value">The value.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the value is saved.</returns>
  private Task SaveAsync(string key, string value, CancellationToken cancellationToken) =>
    store.SaveAsync(SettingDefinitions.Find(key)!, value, cancellationToken);

  /// <summary>Protects a secret and saves it.</summary>
  /// <param name="key">The key of the setting.</param>
  /// <param name="secret">The secret, in clear.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the protected value is saved.</returns>
  private Task SaveSecretAsync(string key, string secret, CancellationToken cancellationToken) =>
    SaveAsync(key, protector.Protect(secret), cancellationToken);

  #endregion

  #region Implementation of ICommandHandler<CompleteGitHubAppManifestCommand,Unit>

  /// <inheritdoc />
  /// <remarks>
  ///   Every setting is checked before GitHub is called, since its code can be used only once. The identifier of the App
  ///   is saved last: it is what closes the wizard, so a failure in the middle leaves it open.
  /// </remarks>
  /// <exception cref="UnauthorizedException">The token is not valid.</exception>
  /// <exception cref="ConflictException">The App already exists (<see cref="SettingErrors.GitHubAppAlreadyCreated" />).</exception>
  /// <exception cref="UnprocessableEntityException">A stronger source provides one of the settings.</exception>
  /// <exception cref="BadRequestException">GitHub does not know the code (<see cref="SettingErrors.InvalidManifestCode" />).</exception>
  public async ValueTask<Unit> Handle(CompleteGitHubAppManifestCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    SetupRules.ThrowIfInvalidToken(issuer, command.SetupToken);

    if (await SetupRules.IsAppCreatedAsync(store, cancellationToken).ConfigureAwait(false))
    {
      throw new ConflictException(SettingErrors.GitHubAppAlreadyCreated, AlreadyCreatedMessage);
    }

    foreach (var key in WrittenKeys)
    {
      await SettingRules.GetEditableAsync(store, SettingDefinitions.Find(key)!, cancellationToken).ConfigureAwait(false);
    }

    var app = await gateway.ConvertManifestAsync(command.Code, cancellationToken).ConfigureAwait(false)
              ?? throw new BadRequestException(SettingErrors.InvalidManifestCode, InvalidCodeMessage);

    await SaveAsync(SettingDefinitions.GitHubClientId, app.ClientId, cancellationToken).ConfigureAwait(false);
    await SaveAsync(SettingDefinitions.GitHubAppSlug, app.Slug, cancellationToken).ConfigureAwait(false);
    await SaveSecretAsync(SettingDefinitions.GitHubClientSecret, app.ClientSecret, cancellationToken).ConfigureAwait(false);
    await SaveSecretAsync(SettingDefinitions.GitHubWebhookSecret, app.WebhookSecret, cancellationToken).ConfigureAwait(false);
    await SaveSecretAsync(SettingDefinitions.GitHubPrivateKeyPem, app.PrivateKeyPem, cancellationToken).ConfigureAwait(false);
    await SaveAsync(SettingDefinitions.GitHubAppId, app.AppId, cancellationToken).ConfigureAwait(false);

    await auditLog.RecordAsync(AuditAction, app.AppId, null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
