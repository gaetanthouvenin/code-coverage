// ------------------------------------------------------------------------------------------------
// <copyright file="ReplaceSecretCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Settings.Commands.ReplaceSecret;

/// <summary>Handler for the <see cref="ReplaceSecretCommand" /> command.</summary>
/// <param name="store">The settings store.</param>
/// <param name="protector">The protector of the secrets.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class ReplaceSecretCommandHandler(
  ISettingsStore store,
  ISecretProtector protector,
  IAuditLog auditLog) : ICommandHandler<ReplaceSecretCommand>
{
  #region Fields

  /// <summary>The audit action of a replaced secret.</summary>
  internal const string AuditAction = "Settings.ReplaceSecret";

  #endregion

  #region Implementation of ICommandHandler<ReplaceSecretCommand,Unit>

  /// <inheritdoc />
  /// <remarks>The store only receives the protected value; the audit entry holds no value at all.</remarks>
  /// <exception cref="NotFoundException">The key is not in the catalog (<see cref="SettingErrors.Unknown" />).</exception>
  /// <exception cref="UnprocessableEntityException">
  ///   The setting is not a secret (<see cref="SettingErrors.NotSecret" />), or a stronger source
  ///   provides its value (<see cref="SettingErrors.ProvidedByEnvironment" />).
  /// </exception>
  public async ValueTask<Unit> Handle(ReplaceSecretCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var definition = SettingRules.FindOrThrow(command.Key);

    if (!definition.IsSecret)
    {
      throw new UnprocessableEntityException(
        SettingErrors.NotSecret,
        $"The setting '{definition.Key}' is not a secret: change its value instead."
      );
    }

    await SettingRules.GetEditableAsync(store, definition, cancellationToken).ConfigureAwait(false);

    var protectedValue = protector.Protect(command.Value);

    await store.SaveAsync(definition, protectedValue, cancellationToken).ConfigureAwait(false);
    await auditLog.RecordAsync(AuditAction, definition.Key, null, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
