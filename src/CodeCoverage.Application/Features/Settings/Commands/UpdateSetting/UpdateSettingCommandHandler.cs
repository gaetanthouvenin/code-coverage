// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateSettingCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Settings.Commands.UpdateSetting;

/// <summary>Handler for the <see cref="UpdateSettingCommand" /> command.</summary>
/// <param name="store">The settings store.</param>
/// <param name="auditLog">The audit trail.</param>
internal sealed class UpdateSettingCommandHandler(ISettingsStore store, IAuditLog auditLog)
  : ICommandHandler<UpdateSettingCommand>
{
  #region Fields

  /// <summary>The audit action of a changed setting.</summary>
  internal const string AuditAction = "Settings.Update";

  #endregion

  #region Implementation of ICommandHandler<UpdateSettingCommand,Unit>

  /// <inheritdoc />
  /// <exception cref="NotFoundException">
  ///   The key is not in the catalog, or its value is set per organization
  ///   (<see cref="SettingErrors.Unknown" />).
  /// </exception>
  /// <exception cref="UnprocessableEntityException">
  ///   The setting is a secret (<see cref="SettingErrors.IsSecret" />), or a stronger source provides
  ///   its value (<see cref="SettingErrors.ProvidedByEnvironment" />).
  /// </exception>
  public async ValueTask<Unit> Handle(UpdateSettingCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var definition = SettingRules.FindInstanceOrThrow(command.Key);

    if (definition.IsSecret)
    {
      throw new UnprocessableEntityException(
        SettingErrors.IsSecret,
        $"The setting '{definition.Key}' is a secret: replace it instead."
      );
    }

    var previous = await SettingRules.GetEditableAsync(store, definition, cancellationToken)
                                     .ConfigureAwait(false);
    var value = SettingRules.Normalize(definition, command.Value);

    await store.SaveAsync(definition, value, cancellationToken).ConfigureAwait(false);

    var details = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["Value"] = value,
      ["PreviousValue"] = previous.Value ?? string.Empty,
      ["PreviousSource"] = previous.Source.ToString(),
    };

    await auditLog.RecordAsync(AuditAction, definition.Key, details, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
